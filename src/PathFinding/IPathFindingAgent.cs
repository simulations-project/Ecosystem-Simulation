﻿using Simulation_CSharp.Tiles;

namespace Simulation_CSharp.PathFinding;

public interface IPathFindingAgent<T> where T : Node
{ 
    void Init(Node start, Node end, Dictionary<TileCell, T> map);

    List<TileCell> FindPath();
    
    List<TileCell> FindPath(Node start, Node end, Dictionary<TileCell, T> map);
}