﻿using System.Numerics;
using Raylib_cs;
using Simulation_CSharp.Core;
using Simulation_CSharp.Entities.AI;
using Simulation_CSharp.Entities.Inheritance;
using Simulation_CSharp.Tiles;
using Simulation_CSharp.Utils;
using Simulation_CSharp.Utils.Widgets;
using Simulation_CSharp.World;

namespace Simulation_CSharp.Entities;

public abstract class Entity
{
    protected readonly Lazy<Brain> Brain;
    public readonly Gene Genetics;
    public readonly bool IsBaby;
    public TileCell Position = null!;
    public ILevel Level = null!;

    public int Health;
    public int Hunger;
    public int Thirst;
    public int ReproductiveUrge;

    public readonly string EntityName;
    public readonly Guid Uuid;
    public bool IsSelected;

    public virtual Lazy<string> TexturePath => new(() => "entities\\" + EntityName.FileSafeFormat() + ".png");

    protected Entity(Gene genetics, string entityName, bool isBaby = false)
    {
        Genetics = genetics;
        EntityName = entityName;
        IsBaby = isBaby;
        Uuid = Guid.NewGuid();
        Brain = new Lazy<Brain>(() =>
        {
            var brain = CreateBrain();
            brain.FinishRegistering();
            return brain;
        });
        Genetics.InfluenceStats(this);
    }

    /// <summary>
    /// Called by ReproduceGoal to make babies
    /// </summary>
    public abstract void MakeBaby(Entity mate);
    
    protected abstract Brain CreateBrain();

    public virtual void Render()
    {
        var texture = ResourceLoader.GetTexture(TexturePath.Value);
        var mouseOver = Helper.IsMousePosOverArea(Position.TruePosition, texture.width, texture.height);
        Raylib.DrawTexture(texture, (int) Position.TruePosition.X, (int) Position.TruePosition.Y, GetRenderColor(mouseOver));
        RenderHoverTooltip(texture, mouseOver);
    }

    public virtual void Update()
    {
        if (SimulationCore.Time == 0)
        {
            return;
        }
        
        Brain.Value.Update();

        if (Health <= 0)
        {
            Destroy();
            Raylib.TraceLog(TraceLogLevel.LOG_INFO, EntityName + " Died");
            return;
        }
        
        if (Helper.Chance(1 * SimulationCore.Time))
        {
            if (ReproductiveUrge < 100)
            {
                ReproductiveUrge += 1 * SimulationCore.Time * Genetics.ReproductiveUrgeModifier;
                ReproductiveUrge = Math.Clamp(ReproductiveUrge, 0, 100);
            }
        }

        if (IsBelowRequirement(Thirst) || IsBelowRequirement(Hunger))
        {
            if (Helper.Chance(2 * SimulationCore.Time))
            {
                Health -= 1 * SimulationCore.Time;
            }
        }

        if (Position.X is > World.Level.WorldWidth or < 0 || Position.Y is > World.Level.WorldHeight or < 0)
        {
            Destroy();
        }

        if (IsSelected)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_DELETE))
            {
                Destroy();
            }
        }
    }

    protected virtual bool IsBelowRequirement(int value)
    {
        return value < 10 / Genetics.MaxConstitution;
    }
    
    public virtual bool IsBelowTolerance(int value)
    {
        return value < 35 / Genetics.MaxConstitution;
    }

    public bool SameSpecieAs(Entity entity)
    {
        return EntityName.Equals(entity.EntityName);
    }
    
    public void Destroy()
    {
        Level.RemoveEntity(this);
    }
    
    public List<TileCell> FindPathTo(TileType type, Predicate<Tile>? match = null)
    {
        var tc = FindTile(type, match);
        var map = Level.GetMap();
        if (tc is null) return new List<TileCell>();
        Brain.Value.PathFinder.Init(map.GetTileAtCell(Position)!, map.GetTileAtCell(tc)!, map.GetGrid());
        return Brain.Value.PathFinder.FindPath();
    }
    
    public List<TileCell> FindPathTo(Predicate<Entity> match)
    {
        var entity = FindEntity(match);
        if (entity is null) return new List<TileCell>();
        var map = Level.GetMap();
        Brain.Value.PathFinder.Init(map.GetTileAtCell(Position)!, map.GetTileAtCell(entity.Position)!, map.GetGrid());
        return Brain.Value.PathFinder.FindPath();
    }
    
    public List<TileCell> FindPathTo(Entity match)
    {
        var map = Level.GetMap();
        Brain.Value.PathFinder.Init(map.GetTileAtCell(Position)!, map.GetTileAtCell(match.Position)!, map.GetGrid());
        return Brain.Value.PathFinder.FindPath();
    }

    /// <summary>
    /// Moves entity towards the target location give
    /// </summary>
    /// <param name="position"></param>
    /// <returns>Returns false if entity can not walk on target location.</returns>
    public bool MoveTowardsLocation(Vector2 position)
    {
        var speed = 0.4F * Genetics.MaxSpeed * SimulationCore.Time;
        var targetPos = new TileCell(position);

        // can not walk out of map
        if (!Level.GetMap().ExistInRange(targetPos.X, targetPos.Y))
        {
            return false;
        }

        var targetTile = Level.GetMap().GetTileAtCell(targetPos);

        if (targetTile is not null && !targetTile.WalkableForEntity(this))
        {
            // if entity can not walk on target position we will stop moving
            return false;
        }

        Position = new TileCell(Helper.MoveTowards(Position.TruePosition, position, speed));

        if (Helper.Chance(2))
        {
            Thirst-=1*SimulationCore.Time;
        }
        else if (Helper.Chance(2))
        {
            Hunger-=1*SimulationCore.Time;
        }

        return true;
    }

    protected Color GetRenderColor(bool mouseOver)
    {
        if (mouseOver && !IsSelected || IsSelected && !mouseOver)
        {
            return Color.DARKGRAY;
        }

        if (mouseOver && IsSelected)
        {
            return Color.BLACK;
        }

        return Color.WHITE;
    }

    protected void RenderHoverTooltip(Texture2D texture2D, bool mouseOver)
    {
        if (mouseOver)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
            {
                IsSelected = !IsSelected;
            }
        }

        if (!IsSelected && !mouseOver) return;

        var rectX = Position.TruePosition.X + 40;
        var rectY = Position.TruePosition.Y - 45;

        var tooltipRenderer = new TooltipRenderer(rectX, rectY, 200, 10);

        tooltipRenderer.DrawText(EntityName + " - " + Genetics.BiologicalSex);
        tooltipRenderer.DrawText(Brain.Value.GetStatus() + "..");
        tooltipRenderer.DrawSpace(5);
        tooltipRenderer.DrawText("Speed: " + Genetics.MaxSpeed);
        tooltipRenderer.DrawText("Sensor Range: " + Genetics.MaxSensorRange);
        tooltipRenderer.DrawText("Constitution: " + Genetics.MaxConstitution);
        tooltipRenderer.DrawSpace(5);
        tooltipRenderer.DrawProgressBar("Health", Genetics.MaxHealth, Health);
        tooltipRenderer.DrawProgressBar("Hunger", Genetics.MaxHunger, Hunger);
        tooltipRenderer.DrawProgressBar("Thirst", Genetics.MaxThirst, Thirst);
        if (!IsBaby)
        {
            tooltipRenderer.DrawProgressBar("Reproductive Urge", Gene.MaxReproductiveUrge, ReproductiveUrge);
        }
        RenderAdditionalTooltip(tooltipRenderer);
        tooltipRenderer.DrawBackground();
    }

    protected virtual void RenderAdditionalTooltip(TooltipRenderer renderer)
    {
    }
    
    /// <summary>
    /// Find the closest tile of type in Entity's Sensor Range
    /// </summary>
    /// <param name="tileType">The tile type looking for.</param>
    /// <param name="match">Additional checking to see if tile found is a match</param>
    /// <returns>The position of the closest tile, returns null if not found.</returns>
    public TileCell? FindTile(TileType tileType, Predicate<Tile>? match = null)
    {
        var range = Genetics.MaxSensorRange / 2;
        TileCell? bestSoFar = null;

        for (var x = -range; x < range; x++)
        {
            for (var y = -range; y < range; y++)
            {
                if (!Level.GetMap().ExistInRange(Position.X + x, Position.Y + y)) continue;
                var pos = new TileCell(Position.X + x, Position.Y + y);

                var tile = tileType.IsDecoration
                    ? Level.GetMap().GetDecorationAtCell(pos)
                    : Level.GetMap().GetTileAtCell(pos);

                if (tile is null || tile.Type != tileType) continue;

                if (bestSoFar is null && (match is null || match.Invoke(tile)))
                {
                    bestSoFar = tile.Position;
                }
                else if (bestSoFar is not null && tile.Position.Distance(Position) < bestSoFar.Distance(Position) && (match is null || match.Invoke(tile)))
                {
                    bestSoFar = tile.Position;
                }
            }
        }

        return bestSoFar;
    }
    
    /// <summary>
    /// Locates the closest tile in the given list
    /// </summary>
    /// <param name="cells"></param>
    /// <returns></returns>
    public TileCell ClosestTileCell(List<TileCell> cells)
    {
        TileCell? closest = null;

        foreach (var cell in cells)
        {
            if (closest is null || Position.Distance(cell) < Position.Distance(closest))
            {
                closest = cell;
            }
        }

        return closest!;
    }

    /// <summary>
    /// Find the closest entity of type in Entity's Sensor Range
    /// </summary>
    /// <param name="match">The entity looking for.</param>
    /// <returns>The closest entity, returns null if not found.</returns>
    public Entity? FindEntity(Predicate<Entity> match)
    {
        var range = Genetics.MaxSensorRange / 2;
        Entity? selected = null;
        
        foreach (var entityInRange in Level.GetEntities().Where((ent, _) => Helper.IsPosInRange(ent.Position, Position, range)))
        {
            if (match(entityInRange))
            {
                selected = entityInRange;
            }    
        }

        return selected;
    }

    protected bool Equals(Entity other)
    {
        return EntityName == other.EntityName && Uuid.Equals(other.Uuid);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Entity) obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(EntityName, Uuid);
    }
    
    public static bool operator ==(Entity? a, Entity? b)
    {
        return a is not null && b is not null && a.EntityName.Equals(b.EntityName) && a.Uuid.Equals(b.Uuid);
    }
    
    public static bool operator !=(Entity? a, Entity? b)
    {
        return a is null || b is null || !a.EntityName.Equals(b.EntityName) || !a.Uuid.Equals(b.Uuid);
    }
}