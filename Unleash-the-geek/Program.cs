using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;


enum EntityType
{
	NONE = -1, MY_ROBOT = 0, OPPONENT_ROBOT = 1, RADAR = 2, TRAP = 3, ORE = 4
}

class Cell
{
	public int Ore { get; set; }
	public bool Hole { get; set; }
	public bool Known { get; set; }

	public void Update(string ore, int hole)
	{
		Hole = hole == 1;
		Known = !"?".Equals(ore);
		if(Known)
		{
			Ore = int.Parse(ore);
		}
	}
}

class Game
{
	// Given at startup
	public readonly int Width;
	public readonly int Height;

	// Updated each turn
	public Dictionary<int, Robot> MyRobots { get; set; }
	public Dictionary<int, Robot> OpponentRobots { get; set; }
	public Cell[,] Cells { get; set; }
	public int RadarCooldown { get; set; }
	public int TrapCooldown { get; set; }
	public int MyScore { get; set; }
	public int OpponentScore { get; set; }
	public List<Entity> Radars { get; set; } = new List<Entity>();
	public List<Entity> Traps { get; set; } = new List<Entity>();
	public List<OreVeil> OreVeils { get; set; } = new List<OreVeil>();

	public int? robotWithTrapId;

	public Game(int width, int height)
	{
		Width = width;
		Height = height;
		MyRobots = new Dictionary<int, Robot>();
		OpponentRobots = new Dictionary<int, Robot>();
		Cells = new Cell[width, height];
		Radars = new List<Entity>();
		Traps = new List<Entity>();

		for(int x = 0; x < width; ++x)
		{
			for(int y = 0; y < height; ++y)
			{
				Cells[x, y] = new Cell();
			}
		}
	}

	public static void AddOrUpdateRobotInfo(Dictionary<int, Robot> collection, Robot src)
	{
		if(collection.TryGetValue(src.Id, out var robot))
		{
			robot.Item = src.Item;
			robot.Pos = src.Pos;
		} else
			collection[src.Id] = src;
	}
}

class Coord
{
	public static readonly Coord NONE = new Coord(-1, -1);

	public int X { get; }
	public int Y { get; }

	public Coord(int x, int y)
	{
		X = x;
		Y = y;
	}

	// Manhattan distance (for 4 directions maps)
	// see: https://en.wikipedia.org/wiki/Taxicab_geometry
	public int Distance(Coord other)
	{
		return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
	}


	public static bool operator ==(Coord obj1, Coord obj2) => Equals2(obj1, obj2);
	public static bool operator !=(Coord obj1, Coord obj2) => !Equals2(obj1, obj2);
	public override bool Equals(object obj) => Equals2(this, (Coord)obj);
	protected bool Equals(Coord other) => Equals2(this, other);
	protected static bool Equals2(Coord obj1, Coord obj2)
	{
		if(Object.ReferenceEquals(obj1, obj2))
			return true;
		if(Object.ReferenceEquals(null, obj2) || Object.ReferenceEquals(obj1, null))
			return false;
		return obj1.X == obj2.X && obj1.Y == obj2.Y;
	}

	public override int GetHashCode()
	{
		return 31 * (31 + X) + Y;
	}
}

class OreVeil
{
	public Coord Pos { get; set; }
	public int Count { get; set; }
	public OreVeil(Coord pos, int count)
	{
		Pos = pos;
		Count = count;
	}
}
class Entity
{
	public int Id { get; set; }
	public Coord Pos { get; set; }
	public EntityType Item { get; set; }

	public Entity(int id, Coord pos, EntityType item)
	{
		Id = id;
		Pos = pos;
		Item = item;
	}
}

class Robot : Entity
{
	public Robot(int id, Coord pos, EntityType item) : base(id, pos, item)
	{ }

	bool IsDead()
	{
		return Pos.Equals(Coord.NONE);
	}

	public static string Wait(string message = "")
	{
		return $"WAIT {message}";
	}

	public static string Move(Coord pos, string message = "")
	{
		return $"MOVE {pos.X} {pos.Y} {message}";
	}

	public static string Dig(Coord pos, string message = "")
	{
		return $"DIG {pos.X} {pos.Y} {message}";
	}

	public static string Request(EntityType item, string message = "")
	{
		return $"REQUEST {item} {message}";
	}

	public string Log(string message) => $"{this.Id} {message}";
}

/**
 * Deliver more ore to hq (left side of the map) than your opponent. Use radars to find ore but beware of traps!
 **/
class Player
{
	static void Main(string[] args)
	{
		new Player();
	}

	Game game;

	public Player()
	{
		string[] inputs;
		inputs = Console.ReadLine().Split(' ');
		int width = int.Parse(inputs[0]);
		int height = int.Parse(inputs[1]); // size of the map

		game = new Game(width, height);

		// game loop
		while(true)
		{
			inputs = Console.ReadLine().Split(' ');
			game.MyScore = int.Parse(inputs[0]); // Amount of ore delivered
			game.OpponentScore = int.Parse(inputs[1]);
			game.OreVeils.Clear();
			for(int i = 0; i < height; i++)
			{
				inputs = Console.ReadLine().Split(' ');
				for(int j = 0; j < width; j++)
				{
					string ore = inputs[2 * j]; // amount of ore or "?" if unknown
					int hole = int.Parse(inputs[2 * j + 1]); // 1 if cell has a hole
					game.Cells[j, i].Update(ore, hole);
					if(game.Cells[j, i].Known && game.Cells[j, i].Ore > 0)
						game.OreVeils.Add(new OreVeil(new Coord(j, i), int.Parse(ore)));
				}
			}

			inputs = Console.ReadLine().Split(' ');
			int entityCount = int.Parse(inputs[0]); // number of entities visible to you
			int radarCooldown = int.Parse(inputs[1]); // turns left until a new radar can be requested
			int trapCooldown = int.Parse(inputs[2]); // turns left until a new trap can be requested
			game.Radars.Clear();
			game.Traps.Clear();

			for(int i = 0; i < entityCount; i++)
			{
				inputs = Console.ReadLine().Split(' ');
				int id = int.Parse(inputs[0]); // unique id of the entity
				EntityType
					type = (EntityType)int.Parse(
						inputs[1]); // 0 for your robot, 1 for other robot, 2 for radar, 3 for trap
				int x = int.Parse(inputs[2]);
				int y = int.Parse(inputs[3]); // position of the entity
				EntityType
					item = (EntityType)int.Parse(
						inputs[4]); // if this entity is a robot, the item it is carrying (-1 for NONE, 2 for RADAR, 3 for TRAP, 4 for ORE)
				Coord coord = new Coord(x, y);
				switch(type)
				{
					case EntityType.MY_ROBOT:
						Game.AddOrUpdateRobotInfo(game.MyRobots, new Robot(id, coord, item));
						break;
					case EntityType.OPPONENT_ROBOT:
						Game.AddOrUpdateRobotInfo(game.OpponentRobots, new Robot(id, coord, item));
						break;
					case EntityType.RADAR:
						game.Radars.Add(new Entity(id, coord, item));
						break;
					case EntityType.TRAP:
						game.Traps.Add(new Entity(id, coord, item));
						break;
				}
			}

			List<string> actionList = new List<String>();
			var enumerator = game.MyRobots.GetEnumerator();
			enumerator.MoveNext();
			for(int i = 0; i < 5; i++)
			{
				// To debug: Console.Error.WriteLine("Debug messages...");
				Robot robot = enumerator.Current.Value;
				enumerator.MoveNext();// game.MyRobots[i];

				string action = Robot.Wait("C# Starter");
				int botWithTrapId;
				//LOGIC block

				if(game.robotWithTrapId != null && !game.MyRobots.Any(x => x.Value.Id == game.robotWithTrapId))
				{
					game.robotWithTrapId = null;
				}

				var needToRequestTrapValue = needToRequestTrap(out botWithTrapId);
				Console.Error.WriteLine("Need trap {0}", needToRequestTrapValue);
				Console.Error.WriteLine("robotWithTrapId {0}", game.robotWithTrapId);

				if(i == 0)
				{
					action = RadarPlacer(robot, radarCooldown, game);
				} else if(needToRequestTrap(out botWithTrapId))
				{
					game.robotWithTrapId = botWithTrapId;
					action = Robot.Request(EntityType.TRAP, "Requesting trap");
				} else if(robot.Item == EntityType.TRAP)
				{
					action = MinePlacer(robot);
				} else
				{
					action = DiggerIssue(robot, game);
				}
				// Implement action selection logic here.

				// WAIT|MOVE x y|REQUEST item|DIG x y
				actionList.Add(action);
			}
			actionList.ForEach(Console.WriteLine);
		}
	}

	bool needToRequestTrap(out int robotId)
	{
		robotId = default;
		if(game.MyRobots.Any(x => x.Value.Item == EntityType.TRAP))
		{
			return false;
		}
		var bot = game.MyRobots.Select(x => x.Value)
			  .OrderBy(x => x.Pos.X).FirstOrDefault(x => x.Pos.X == 0);
		if(bot != null)
		{
			robotId = bot.Id;
			return true;
		}
		return false;
	}
	string MinePlacer(Robot robot)
	{
		var minePlacer = game.MyRobots.Select(x => x.Value).Where(x => x.Id == (int)game.robotWithTrapId).FirstOrDefault();

		var oreVeilToPlace = game.OreVeils.OrderBy(x => x.Pos.Distance(minePlacer.Pos))
			.FirstOrDefault();
		if(oreVeilToPlace == null)
		{
			return DiggerIssue(robot, game);
		}
		var coords = oreVeilToPlace.Pos;

		if(oreVeilToPlace.Pos.Distance(minePlacer.Pos) < 2)
		{
			return Robot.Dig(coords, "Placing trap");
		} else
			return (Robot.Move(coords, $"Moving to ({coords.X}, {coords.Y}) for placing trap"));
	}

	string RadarPlacer(Robot robot, int radarCooldown, Game game)
	{
		Console.Error.WriteLine(robot.Item);
		if(robot.Item == EntityType.RADAR)
		{
			var coords = GetPointForRadarPlacer(robot, game.Radars);
			if(coords == null)
				return DiggerIssue(robot, game);
			if(!robot.Pos.Equals(coords))
			{
				return Robot.Move(coords, $"Moving to {coords.X}, {coords.Y}");
			}
			return Robot.Dig(coords, "Placing radar");
		}
		if(robot.Item == EntityType.NONE || robot.Item == EntityType.ORE)
		{
			return Robot.Request(EntityType.RADAR, "Requesting radar");
		}
		return Robot.Wait(robot.Log("Waiting for best time..."));
	}

	//VLAD's method
	Coord GetPointForRadarPlacer(Robot robot, List<Entity> radars)
	{
		var list = new[] {
			new Coord(6, 5),
			new Coord(11, 9),
			new Coord(16, 5),
			new Coord(21, 9),
			new Coord(26, 5),
			new Coord(16, 13),
			new Coord(7, 14),
			new Coord(25, 14),
			new Coord(1, 10),//maybe change with next
			new Coord(30, 10),
			new Coord(11, 1),
			new Coord(21, 1),
			new Coord(30, 1),//maybe change with next
			new Coord(1, 1),
			new Coord(11, 15),
			new Coord(19, 15),
			new Coord(29, 15),
};
		return list.Select(x => new Coord(x.X - 1, x.Y - 1)).FirstOrDefault(x => radars.All(r => r.Pos != x));
	}

	string DiggerIssue(Robot robot, Game game)
	{
		switch(robot.Item)
		{
			case EntityType.ORE:
				return Robot.Move(new Coord(0, robot.Pos.Y), "Return Ore");

			case EntityType.NONE:
			default:
				var closest = game.OreVeils.Where(x => game.Traps.All(t => t.Pos != x.Pos)).OrderBy(x => x.Pos.Distance(robot.Pos)).FirstOrDefault();
				if(closest == null)
					return Robot.Wait("TODO 0");

				var distance = closest.Pos.Distance(robot.Pos);
				if(distance < 2)
					return Robot.Dig(closest.Pos, "Kopai!");

				return Robot.Move(closest.Pos, $"Begy k ({closest.Pos.X},{closest.Pos.Y})");
		}
	}
}
