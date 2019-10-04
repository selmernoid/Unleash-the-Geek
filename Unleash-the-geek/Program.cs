﻿using System;
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
        if (Known)
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
    public List<Entity> OreVeils { get; set; } = new List<Entity>();

    public Game(int width, int height)
    {
        Width = width;
        Height = height;
        MyRobots = new Dictionary<int, Robot>();
        OpponentRobots = new Dictionary<int, Robot>();
        Cells = new Cell[width, height];
        Radars = new List<Entity>();
        Traps = new List<Entity>();

        for (int x = 0; x < width; ++x)
        {
            for (int y = 0; y < height; ++y)
            {
                Cells[x, y] = new Cell();
            }
        }
    }

    public static void AddOrUpdateRobotInfo(Dictionary<int, Robot> collection, Robot src)
    {
        if (collection.Keys.Contains(src.Id))
        {
            var robot = collection[src.Id];
            robot.Item = src.Item;
            robot.Pos = src.Pos;
        }
        else
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

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;
        if (this.GetType() != obj.GetType())
            return false;
        Coord other = (Coord)obj;
        return X == other.X && Y == other.Y;
    }

    public override int GetHashCode()
    {
        return 31 * (31 + X) + Y;
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
        while (true)
        {
            inputs = Console.ReadLine().Split(' ');
            game.MyScore = int.Parse(inputs[0]); // Amount of ore delivered
            game.OpponentScore = int.Parse(inputs[1]);
            for (int i = 0; i < height; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                for (int j = 0; j < width; j++)
                {
                    string ore = inputs[2 * j]; // amount of ore or "?" if unknown
                    int hole = int.Parse(inputs[2 * j + 1]); // 1 if cell has a hole
                    game.Cells[j, i].Update(ore, hole);
                }
            }

            inputs = Console.ReadLine().Split(' ');
            int entityCount = int.Parse(inputs[0]); // number of entities visible to you
            int radarCooldown = int.Parse(inputs[1]); // turns left until a new radar can be requested
            int trapCooldown = int.Parse(inputs[2]); // turns left until a new trap can be requested
            game.Radars.Clear();
            game.Traps.Clear();
            game.OreVeils.Clear();

            for (int i = 0; i < entityCount; i++)
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
                switch (type)
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
                    case EntityType.ORE:
                        game.OreVeils.Add(new Entity(id, coord, item));
                        break;
                }
            }

            List<string> actionList = new List<String>();
            for (int i = 0; i < 5; i++)
            {
                // To debug: Console.Error.WriteLine("Debug messages...");
                Robot robot = game.MyRobots[i];

                string action = Robot.Wait("C# Starter");

                //LOGIC block
                if (i == 0)
                {
                    action = RadarPlacer(robot, radarCooldown);
                }
                else
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

    string RadarPlacer(Robot robot, int radarCooldown)
    {
        Console.Error.WriteLine(robot.Item);
        if (robot.Item == EntityType.RADAR)
        {
            var coords = GetPointForRadarPlacer(robot);
            if (!robot.Pos.Equals(coords))
            {
                return Robot.Move(coords, $"Moving to {coords.X}, {coords.Y}");
            }
            return Robot.Dig(coords, "Placing radar");
        }
        if (robot.Item == EntityType.NONE)
        {
            return Robot.Request(EntityType.RADAR, "Requesting radar");
        }
        return Robot.Wait(robot.Log("Waiting for best time..."));
    }

    //VLAD's method
    Coord GetPointForRadarPlacer(Robot robot)
    {
        return new Coord(4, 2);
    }

    string DiggerIssue(Robot robot, Game game)
    {
        switch (robot.Item)
        {
            case EntityType.ORE: return Robot.Move(new Coord(0, robot.Pos.Y), "Return Ore");

            case EntityType.NONE:
            default:
                var closest = game.OreVeils.OrderBy(x => x.Pos.Distance(robot.Pos)).FirstOrDefault();
                if (closest == null)
                    return Robot.Wait("TODO 0");

                var distance = closest.Pos.Distance(robot.Pos);
                if (distance < 2)
                    return Robot.Dig(closest.Pos, "Kopai!");

                return Robot.Move(closest.Pos, $"Begy k ({closest.Pos.X},{closest.Pos.Y})");
        }
    }
}