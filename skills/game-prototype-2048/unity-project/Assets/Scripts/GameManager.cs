using System;
using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>4x4 grid 2048 game state.  Singleton.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int Score { get; private set; }
        public bool IsGameOver { get; private set; }
        public event Action<int> OnScoreChanged;
        public event Action<bool> OnGameOverChanged;

        public const int N = 4;
        public int[,] grid = new int[N, N];

        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private float cellSize = 1.2f;

        // GO refs per cell for tile sprites
        private GameObject[,] tileGOs = new GameObject[N, N];

        public void SetRefs(GameObject prefab, Transform root, float cell)
        { tilePrefab = prefab; gridRoot = root; cellSize = cell; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            SpawnRandom();
            SpawnRandom();
            Refresh();
        }

        public Vector3 CellPos(int x, int y)
        {
            // grid origin top-left, x goes right, y goes down
            float ox = -(N - 1) / 2f * cellSize;
            float oy =  (N - 1) / 2f * cellSize;
            return new Vector3(ox + x * cellSize, oy - y * cellSize, 0);
        }

        /// <summary>0=left 1=right 2=up 3=down</summary>
        public void TryMove(int dir)
        {
            if (IsGameOver) return;
            bool moved = false;
            int gained = 0;
            // Reduce all 4 directions to "slide-left" by transposing/reversing
            for (int line = 0; line < N; line++)
            {
                int[] row = ExtractLine(line, dir);
                var (newRow, scoreDelta, lineMoved) = CollapseLeft(row);
                if (lineMoved) moved = true;
                gained += scoreDelta;
                WriteLine(line, dir, newRow);
            }
            if (moved)
            {
                Score += gained;
                OnScoreChanged?.Invoke(Score);
                if (gained > 0 && AudioBank.Instance != null) AudioBank.Instance.PlayMerge();
                else if (AudioBank.Instance != null) AudioBank.Instance.PlaySlide();
                SpawnRandom();
                Refresh();
                if (!AnyMoveAvailable()) SetGameOver(true);
            }
        }

        // Pull a line as if it were a left-slide row (dir-aware index transform).
        private int[] ExtractLine(int line, int dir)
        {
            int[] row = new int[N];
            for (int i = 0; i < N; i++)
            {
                int gx = 0, gy = 0;
                switch (dir)
                {
                    case 0: gx = i;     gy = line; break;       // left: rows
                    case 1: gx = N-1-i; gy = line; break;       // right: rows reversed
                    case 2: gx = line;  gy = i;    break;       // up: cols
                    case 3: gx = line;  gy = N-1-i; break;      // down: cols reversed
                }
                row[i] = grid[gx, gy];
            }
            return row;
        }

        private void WriteLine(int line, int dir, int[] row)
        {
            for (int i = 0; i < N; i++)
            {
                int gx = 0, gy = 0;
                switch (dir)
                {
                    case 0: gx = i;     gy = line; break;
                    case 1: gx = N-1-i; gy = line; break;
                    case 2: gx = line;  gy = i;    break;
                    case 3: gx = line;  gy = N-1-i; break;
                }
                grid[gx, gy] = row[i];
            }
        }

        private (int[] newRow, int scoreDelta, bool moved) CollapseLeft(int[] row)
        {
            // 1. remove zeros
            var nonzero = new List<int>();
            for (int i = 0; i < row.Length; i++) if (row[i] != 0) nonzero.Add(row[i]);
            // 2. merge adjacent equal pairs
            int scoreDelta = 0;
            var merged = new List<int>();
            int j = 0;
            while (j < nonzero.Count)
            {
                if (j + 1 < nonzero.Count && nonzero[j] == nonzero[j + 1])
                {
                    int v = nonzero[j] * 2;
                    merged.Add(v);
                    scoreDelta += v;
                    j += 2;
                }
                else
                {
                    merged.Add(nonzero[j]);
                    j++;
                }
            }
            // 3. pad with zeros
            while (merged.Count < row.Length) merged.Add(0);
            var newRow = merged.ToArray();
            // 4. moved?
            bool moved = false;
            for (int i = 0; i < row.Length; i++) if (row[i] != newRow[i]) { moved = true; break; }
            return (newRow, scoreDelta, moved);
        }

        private bool AnyMoveAvailable()
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (grid[x, y] == 0) return true;
                    if (x + 1 < N && grid[x, y] == grid[x + 1, y]) return true;
                    if (y + 1 < N && grid[x, y] == grid[x, y + 1]) return true;
                }
            return false;
        }

        private void SpawnRandom()
        {
            var empties = new List<Vector2Int>();
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    if (grid[x, y] == 0) empties.Add(new Vector2Int(x, y));
            if (empties.Count == 0) return;
            var pick = empties[UnityEngine.Random.Range(0, empties.Count)];
            grid[pick.x, pick.y] = UnityEngine.Random.value < 0.9f ? 2 : 4;
        }

        private void Refresh()
        {
            if (tilePrefab == null) return;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int v = grid[x, y];
                    if (v == 0)
                    {
                        if (tileGOs[x, y] != null) { Destroy(tileGOs[x, y]); tileGOs[x, y] = null; }
                        continue;
                    }
                    if (tileGOs[x, y] == null)
                    {
                        var go = Instantiate(tilePrefab, CellPos(x, y), Quaternion.identity, gridRoot);
                        tileGOs[x, y] = go;
                    }
                    var t = tileGOs[x, y].GetComponent<Tile>();
                    if (t != null) t.SetValue(v);
                }
        }

        private void SetGameOver(bool over)
        {
            IsGameOver = over;
            OnGameOverChanged?.Invoke(over);
        }
    }
}
