using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public int mapWidth = 10;
    public int mapHeight = 10;
    public GameObject tilePrefab;
    public GameObject waterPrefab;
    public GameObject beachPrefabA;
    public GameObject beachPrefabB;
    public GameObject beachPrefabC;
    public Transform mapHolder;
    public float tileSize = 1.0f;

    private GameObject[,] _grid;

    void Start()
    {
        _grid = new GameObject[mapWidth, mapHeight];
        MakeMapGrid();
        UpdateBeachTiles();
    }

    private Vector2 GetHexCords(int x, int y)
    {
        float xCord = x * tileSize * Mathf.Cos(Mathf.Deg2Rad * 30);
        float yCord = y * tileSize + ((x % 2 == 1) ? tileSize * 0.5f : 0);

        return new Vector2(xCord, yCord);
    }

    void MakeMapGrid()
    {
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                Vector2 hexCords = GetHexCords(i, j);
                Vector3 tilePosition = new Vector3(hexCords.x, 0, hexCords.y);

                GameObject tileToInstantiate = DetermineTileType(i, j);
                GameObject tile = Instantiate(tileToInstantiate, tilePosition, Quaternion.Euler(0, 30, 0));
                AssignTag(tile, tileToInstantiate);
                _grid[i, j] = tile;
            }
        }
    }

    GameObject DetermineTileType(int x, int y)
    {
        if (x < 2 || y < 2 || x > mapWidth - 3 || y > mapHeight - 3)
        {
            return waterPrefab; // Water at the edges
        }
        else
        {
            return tilePrefab; // Land in the center
        }
    }

    void AssignTag(GameObject tile, GameObject prefab)
    {
        if (prefab == waterPrefab)
        {
            tile.tag = "Water";
        }
        else if (prefab == beachPrefabA || prefab == beachPrefabB || prefab == beachPrefabC)
        {
            tile.tag = "Beach";
        }
        else
        {
            tile.tag = "Land";
        }
    }

    public GameObject GetTileAt(int x, int y)
    {
        if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
        {
            return _grid[x, y];
        }
        return null;
    }

    void UpdateBeachTiles()
    {
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                if (_grid[i, j].CompareTag("Beach"))
                {
                    int landNeighbors = CountLandNeighbors(i, j);
                    UpdateBeachTile(i, j, landNeighbors);
                }
            }
        }
    }

    int CountLandNeighbors(int x, int y)
    {
        int landCount = 0;
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
        };

        foreach (var dir in directions)
        {
            GameObject neighbor = GetTileAt(x + dir.x, y + dir.y);
            if (neighbor != null && neighbor.CompareTag("Land"))
            {
                landCount++;
            }
        }

        return landCount;
    }

    void UpdateBeachTile(int x, int y, int landNeighbors)
    {
        GameObject newBeachPrefab = null;
        switch (landNeighbors)
        {
            case 3:
                newBeachPrefab = beachPrefabA;
                break;
            case 2:
                newBeachPrefab = beachPrefabB;
                break;
            case 1:
                newBeachPrefab = beachPrefabC;
                break;
        }

        if (newBeachPrefab != null)
        {
            Vector3 position = _grid[x, y].transform.position;
            Quaternion rotation = DetermineRotation(x, y);
            Destroy(_grid[x, y]);
            _grid[x, y] = Instantiate(newBeachPrefab, position, rotation);
            _grid[x, y].tag = "Beach";
        }
    }

    Quaternion DetermineRotation(int x, int y)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
        };

        for (int i = 0; i < directions.Length; i++)
        {
            GameObject neighbor = GetTileAt(x + directions[i].x, y + directions[i].y);
            if (neighbor != null && neighbor.CompareTag("Land"))
            {
                return Quaternion.Euler(0, 90 + i * 60, 0);
            }
        }

        return Quaternion.identity;
    }
}