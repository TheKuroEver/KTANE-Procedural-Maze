﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeHandler {

    // Up and Down are swapped because we are using Y going down instead of up.
    private Dictionary<MazeDirection, Vector2Int> _directionVectors = new Dictionary<MazeDirection, Vector2Int> {
        { MazeDirection.Up, Vector2Int.down },
        { MazeDirection.Right, Vector2Int.right },
        { MazeDirection.Down, Vector2Int.up },
        { MazeDirection.Left, Vector2Int.left },
    };

    private ProceduralMazeModule _module;
    private MazeRenderer _renderer;
    private BitMaze6x6 _maze;

    private Vector2Int _currentPosition;
    private Stack<Vector2Int> _visitedCells = new Stack<Vector2Int>();

    public MazeHandler(ProceduralMazeModule module) {
        _module = module;
        _maze = MazeGenerator.GenerateNewMaze();
        _currentPosition = _maze.StartPosition;
        _visitedCells.Push(_currentPosition);

        _renderer = module.GetComponentInChildren<MazeRenderer>();
        _renderer.AssignMaze(_maze);
        _renderer.RenderRings();

        //!
        Debug.Log(_maze.CurrentSeed);
    }

    public void Move(MazeDirection direction) {
        if (_maze.GetAdjacentWallInDirection(_currentPosition, direction).IsPresent) {
            Debug.Log("Bruh");
        }
        else {
            Vector2Int newPosition = _currentPosition + _directionVectors[direction];
            _renderer.RenderMovement(_currentPosition, newPosition);
            _currentPosition = newPosition;

            if (!_visitedCells.Contains(_currentPosition)) {
                MazeGenerator.DecideWallsAroundCell(_maze, _currentPosition, direction);
                _visitedCells.Push(_currentPosition);
            }

            //!
            Debug.Log(_maze.CurrentSeed);
            _renderer.RenderWalls();
        }
    }

}