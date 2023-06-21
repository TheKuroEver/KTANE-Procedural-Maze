﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeRenderer : MonoBehaviour {

    private const float _cellXyOrigin = 0.005f;
    private const float _cellXyOffset = -0.002f;
    private const float _mazeZPosition = 0.00067f;
    private const float _lightIntensity = 17.7f;
    private const float _lightRange = 0.015f;
    private const float _initialLightScale = 5.95f;
    private const float _transitionTime = 0.2f;

    [SerializeField] private GameObject _cell;
    [SerializeField] private GameObject _triangle;
    [SerializeField] private GameObject _wall;
    [SerializeField] private GameObject _ring;
    [SerializeField] private GameObject _grid;

    [SerializeField] private Material _cellOffMat;
    [SerializeField] private Material _cellOnMat;

    // Stored as [column, row].
    private IMazeCell[,] _cells = new IMazeCell[6, 6];
    private MeshRenderer[,] _ringRenderers = new MeshRenderer[6, 6];
    // These are the walls in each row and column. Stored as [column/row, wall].
    private MeshRenderer[,] _columnWallRenderers = new MeshRenderer[6, 7];
    private MeshRenderer[,] _rowWallRenderers = new MeshRenderer[6, 7];

    private BitMaze6x6 _maze;
    private Vector2Int _currentRenderedPosition;

    private void Awake() {
        GenerateWalls();
    }

    private void GenerateWalls() {
        var colRotation = Quaternion.FromToRotation(Vector3.up, Vector3.right);
        var rowRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);

        for (int line = 0; line < 6; line++) {
            for (int wall = 0; wall < 7; wall++) {
                var colWallPos = new Vector3(_cellXyOrigin + line * _cellXyOffset, _cellXyOrigin + (-0.5f + wall) * _cellXyOffset, _mazeZPosition);
                GameObject newColWall = Instantiate(_wall, _grid.transform);
                newColWall.transform.localPosition = colWallPos;
                newColWall.transform.rotation = colRotation;
                _columnWallRenderers[line, wall] = newColWall.GetComponent<MeshRenderer>();

                var rowWallPos = new Vector3(_cellXyOrigin + (-0.5f + wall) * _cellXyOffset, _cellXyOrigin + line * _cellXyOffset, _mazeZPosition);
                GameObject newRowWall = Instantiate(_wall, _grid.transform);
                newRowWall.transform.localPosition = rowWallPos;
                newRowWall.transform.rotation = rowRotation;
                _rowWallRenderers[line, wall] = newRowWall.GetComponent<MeshRenderer>();
            }
        }
    }

    public void AssignMaze(BitMaze6x6 maze) {
        _maze = maze;
        RenderCellsAndGenerateRings(maze.StartCell.Position, maze.GoalCell.Position);
        RenderRings();
        _currentRenderedPosition = maze.StartCell.Position;
    }

    private void RenderCellsAndGenerateRings(Vector2Int start, Vector2Int goal) {
        for (int i = 0; i < 6; i++) {
            for (int j = 0; j < 6; j++) {
                Vector3 position = new Vector3(_cellXyOrigin + i * _cellXyOffset, _cellXyOrigin + j * _cellXyOffset, _mazeZPosition);

                if (i == goal.x && j == goal.y) {
                    _cells[i, j] = new GoalCell(this, _triangle, position, _grid.transform);
                }
                else {
                    _cells[i, j] = new NormalCell(this, _cell, position, _grid.transform, _cellOffMat, _cellOnMat);
                    _cells[i, j].SetLightState(i == start.x && j == start.y);
                }

                GameObject newRing = Instantiate(_ring, _grid.transform);
                newRing.transform.localPosition = position;
                _ringRenderers[i, j] = newRing.GetComponent<MeshRenderer>();
            }
        }
    }

    public void RenderRings() {
        for (int col = 0; col < 6; col++) {
            for (int row = 0; row < 6; row++) {
                _ringRenderers[col, row].enabled = _maze.Cells[col, row].Bit == 1;
            }
        }
    }

    public void RenderMovementTo(Vector2Int position) {
        _cells[position.x, position.y].SetLightState(true);
        _cells[_currentRenderedPosition.x, _currentRenderedPosition.y].SetLightState(false);
        _currentRenderedPosition = position;
    }

    public void RenderWalls() {
        for (int line = 0; line < 6; line++) {
            for (int wall = 0; wall < 7; wall++) {
                BitMaze6x6.Wall colWall = _maze.ColumnWalls[line, wall];
                if (colWall.IsDecided && colWall.IsPresent) {
                    _columnWallRenderers[line, wall].enabled = true;
                }
                else {
                    _columnWallRenderers[line, wall].enabled = false;
                }

                BitMaze6x6.Wall rowWall = _maze.RowWalls[line, wall];
                if (rowWall.IsDecided && rowWall.IsPresent) {
                    _rowWallRenderers[line, wall].enabled = true;
                }
                else {
                    _rowWallRenderers[line, wall].enabled = false;
                }
            }
        }
    }

    private interface IMazeCell {
        void SetLightState(bool setToLit);
    }

    private class NormalCell : IMazeCell {

        private MazeRenderer _parentRenderer;
        private Coroutine _transition;
        private bool _isLit;

        private Material _offMat;
        private Material _onMat;

        private MeshRenderer _renderer;
        private Light _light;

        private float _activeLightRange;

        public NormalCell(MazeRenderer parentRenderer, GameObject prefab, Vector3 position, Transform parent, Material offMat, Material onMat) {
            GameObject cell = Instantiate(prefab, parent);
            cell.transform.localPosition = position;
            _renderer = cell.GetComponent<MeshRenderer>();
            _light = cell.GetComponentInChildren<Light>();
            _light.intensity = 0;
            _offMat = new Material(offMat);
            _onMat = new Material(onMat);
            _parentRenderer = parentRenderer;
            _light.range = parentRenderer.transform.lossyScale.x / _initialLightScale * _lightRange;
        }

        public void SetLightState(bool setToLit) {
            if (_transition != null) {
                _parentRenderer.StopCoroutine(_transition);
            }
            if (setToLit) {
                _transition = _parentRenderer.StartCoroutine(TransitionToColour(Color.white));
            }
            else {
                _transition = _parentRenderer.StartCoroutine(TransitionToOff());
            }
        }

        private IEnumerator TransitionToColour(Color newColour) {
            return TransitionToColour(newColour, newColour);
        }

        private IEnumerator TransitionToColour(Color newColour, Color newCellMatColour) {
            float oldIntensity = _light.intensity;
            float newIntensity = _lightIntensity;
            Color oldColour = _isLit ? _light.color : newColour;
            Color oldMatColour = _renderer.material.color;
            float elapsedTime = 0;

            _isLit = true;

            while (elapsedTime < _transitionTime) {
                elapsedTime += Time.deltaTime;
                _light.intensity = Mathf.Lerp(oldIntensity, newIntensity, elapsedTime / _transitionTime);
                _light.color = Color.Lerp(oldColour, newColour, elapsedTime / _transitionTime);
                _renderer.material.color = Color.Lerp(oldMatColour, newCellMatColour, elapsedTime / _transitionTime);
                yield return null;
            }
            _light.intensity = newIntensity;
            _light.color = newColour;
            _onMat.color = newCellMatColour;

            _renderer.material = _onMat;
        }

        private IEnumerator TransitionToOff() {
            float oldIntensity = _light.intensity;
            float elapsedTime = 0;

            yield return null;
            _isLit = false;
            _renderer.material = _offMat;

            while (elapsedTime < _transitionTime) {
                elapsedTime += Time.deltaTime;
                _light.intensity = Mathf.Lerp(oldIntensity, 0, elapsedTime / _transitionTime);
                yield return null;
            }
            _light.intensity = 0;
        }
    }

    private class GoalCell : IMazeCell {

        private MazeRenderer _parentRenderer;
        private Coroutine _transition;

        private static readonly Color _goalBlue = new Color(0, 0.79f, 1);

        private MeshRenderer _renderer;
        private Light _light;

        public GoalCell(MazeRenderer parentRenderer, GameObject prefab, Vector3 position, Transform parent) {
            GameObject cell = Instantiate(prefab, parent);
            cell.transform.localPosition = position;
            _renderer = cell.GetComponent<MeshRenderer>();
            _light = cell.GetComponentInChildren<Light>();
            _parentRenderer = parentRenderer;
            _light.range = parentRenderer.transform.lossyScale.x / _initialLightScale * _lightRange;
        }

        // Setting the light state to false on the goal cell actually makes it blue (unoccupied).
        public void SetLightState(bool setToLit) {
            if (_transition != null) {
                _parentRenderer.StopCoroutine(_transition);
            }
            _transition = _parentRenderer.StartCoroutine(Transition(setToLit ? Color.white : _goalBlue));
        }

        private IEnumerator Transition(Color newColour) {
            Color oldColour = _light.color;
            float elapsedTime = 0;

            while (elapsedTime < _transitionTime) {
                elapsedTime += Time.deltaTime;
                Color currentColor = Color.Lerp(oldColour, newColour, elapsedTime / _transitionTime);
                _light.color = currentColor;
                _renderer.material.color = currentColor;
                yield return null;
            }
            _light.color = newColour;
            _renderer.material.color = newColour;
        }
    }
}
