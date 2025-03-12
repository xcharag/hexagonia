using System.Collections.Generic;
using Classes;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public int mapWidth = 10;
    public int mapHeight = 10;
    public GameObject waterPrefab;
    public GameObject landPrefab;
    public GameObject forestPrefab;
    public GameObject mountainPrefab;
    public GameObject mountainForestPrefab;
    public Transform mapHolder;
    public float tileSize = 1.0f;

    [Header("Noise Settings")]
    public int seed;

    [Header("Special Tiles")]
    public GameObject chestPrefab;
    public GameObject archeryZonePrefab;
    public GameObject lilyPadPrefab;
    public GameObject podPrefab;
    public int minDistanceBetweenSameType = 3;
    
    [Header("Terrain Distribution")]
    [Range(0, 100)] public float waterPercentage = 35f;    // Water coverage
    [Range(0, 100)] public float landPercentage = 30f;     // Basic land coverage
    [Range(0, 100)] public float forestPercentage = 20f;   // Forest coverage
    [Range(0, 100)] public float mountainPercentage = 10f; // Mountain coverage
    [Range(0, 100)] public float mountainForestPercentage = 5f; // Mountain with forest coverage
    private float _mountainForestThreshold;

    // Constants for world coordinate calculations
    private const float HexXFull = 3.516f;
    private const float HexZFull = 2.03f;

    // Default parameters that will be randomized
    private float _noiseScale;
    private int _octaves;
    private float _persistence;
    private float _lacunarity;
    private Vector2 _offset;
    private float _waterThreshold;
    private float _forestThreshold;
    private float _mountainThreshold;

    // Special tile spawn chances
    private float _chestSpawnChance;      // Very Rare
    private float _archerySpawnChance;    // Rare
    private float _podSpawnChance;        // Uncommon
    private float _lilyPadSpawnChance;    // Common

    private GameObject[,] _grid;
    private Hex[,] _hexGrid;
    private float[,] _heightMap;
    private float[,] _forestMap;
    private int[,] _mountainHeight;
    private System.Random _prng;

    // Track placed special tiles
    private readonly List<Vector2Int> _chestPositions = new List<Vector2Int>();
    private readonly List<Vector2Int> _archeryPositions = new List<Vector2Int>();
    private readonly List<Vector2Int> _lilyPadPositions = new List<Vector2Int>();
    private readonly List<Vector2Int> _podPositions = new List<Vector2Int>();

    void Start()
    {
        _prng = new System.Random(seed);
        
        // Randomize generation parameters based on seed
        RandomizeParameters();
        
        _grid = new GameObject[mapWidth, mapHeight];
        _hexGrid = new Hex[mapWidth, mapHeight];

        GenerateNoiseMaps();
        MakeMapGrid();
        PlaceSpecialTiles();
    }

    void RandomizeParameters()
    {
        // Normalize percentages to ensure they sum to 100%
        float total = waterPercentage + landPercentage + forestPercentage + mountainPercentage + mountainForestPercentage;
        float normalizer = 100f / total;
    
        float waterNorm = waterPercentage * normalizer / 100f;
        float landNorm = landPercentage * normalizer / 100f;
        float forestNorm = forestPercentage * normalizer / 100f;
        float mountainNorm = mountainPercentage * normalizer / 100f;
        float mountainForestNorm = mountainForestPercentage * normalizer / 100f;

        // Noise parameters
        _noiseScale = 15f + (float)_prng.NextDouble() * 30f;
        _octaves = 3 + _prng.Next(4);
        _persistence = 0.3f + (float)_prng.NextDouble() * 0.4f;
        _lacunarity = 1.5f + (float)_prng.NextDouble();
        _offset = new Vector2(_prng.Next(-10000, 10000), _prng.Next(-10000, 10000));

        // Calculate thresholds based on desired percentages
        _waterThreshold = waterNorm;
        _forestThreshold = _waterThreshold + landNorm;
        _mountainThreshold = _forestThreshold + forestNorm;
        _mountainForestThreshold = _mountainThreshold + mountainNorm;
        // Mountain forest is the remainder

        // Special tile spawn chances
        _chestSpawnChance = 0.001f;    // Very Rare
        _archerySpawnChance = 0.009f;  // Rare
        _podSpawnChance = 0.01f;       // Uncommon
        _lilyPadSpawnChance = 0.1f;    // Common

        Debug.Log($"Terrain distribution - Water: {waterNorm*100:F1}%, Land: {landNorm*100:F1}%, " +
                  $"Forest: {forestNorm*100:F1}%, Mountain: {mountainNorm*100:F1}%, " +
                  $"Mountain Forest: {mountainForestNorm*100:F1}%");
    }

    void GenerateNoiseMaps()
    {
        // Generate main height map
        _heightMap = GenerateNoiseMap(mapWidth, mapHeight, seed, _noiseScale, _octaves, _persistence, _lacunarity, _offset);

        // Generate separate map for forest distribution with different seed
        _forestMap = GenerateNoiseMap(mapWidth, mapHeight, seed + 1, _noiseScale * 1.5f, _octaves - 1, _persistence, _lacunarity, new Vector2(_offset.x + 100, _offset.y + 100));

        // Generate mountain heights
        _mountainHeight = new int[mapWidth, mapHeight];
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                if (_heightMap[i, j] > _mountainThreshold)
                {
                    float heightValue = (_heightMap[i, j] - _mountainThreshold) / (1 - _mountainThreshold);

                    // Distribute mountain heights: 1-2 common, 3 uncommon, 4 rare
                    if (heightValue > 0.95f) _mountainHeight[i, j] = 4;      // 5% chance for height 4
                    else if (heightValue > 0.75f) _mountainHeight[i, j] = 3; // 20% chance for height 3
                    else if (heightValue > 0.45f) _mountainHeight[i, j] = 2; // 30% chance for height 2
                    else _mountainHeight[i, j] = 1;                          // 45% chance for height 1
                }
            }
        }
    }

    float[,] GenerateNoiseMap(int width, int height, int seed, float scale, int octaves,
                            float persistence, float lacunarity, Vector2 offset)
    {
        float[,] noiseMap = new float[width, height];

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        if (scale <= 0)
            scale = 0.0001f;

        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;

        float halfWidth = width / 2f;
        float halfHeight = height / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x - halfWidth) / scale * frequency + octaveOffsets[i].x;
                    float sampleY = (y - halfHeight) / scale * frequency + octaveOffsets[i].y;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                if (noiseHeight > maxNoiseHeight)
                    maxNoiseHeight = noiseHeight;
                if (noiseHeight < minNoiseHeight)
                    minNoiseHeight = noiseHeight;

                noiseMap[x, y] = noiseHeight;
            }
        }

        // Normalize noise map
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                noiseMap[x, y] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseMap[x, y]);
            }
        }

        return noiseMap;
    }

    GameObject DetermineTileType(int x, int y)
    {
        float value = _heightMap[x, y]; // Use height map as our distribution value

        // Determine tile type based on thresholds
        if (value < _waterThreshold)
            return waterPrefab;
        else if (value < _forestThreshold)
            return landPrefab;
        else if (value < _mountainThreshold)
            return forestPrefab;
        else if (value < _mountainForestThreshold)
            return mountainPrefab;
        else
            return mountainForestPrefab;
    }

    void MakeMapGrid()
    {
        // Map generation code (existing method)
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                Vector2 hexCords = GetHexCords(i, j);
                Vector3 tilePosition = new Vector3(hexCords.x, 0, hexCords.y);

                GameObject tileToInstantiate = DetermineTileType(i, j);

                // Set mountain height if applicable
                float tileHeight = 0;
                if (_heightMap[i, j] > _mountainThreshold)
                {
                    tileHeight = _mountainHeight[i, j] * 0.5f;
                }

                // Adjust position for height
                tilePosition.y = tileHeight;

                // Instantiate the tile
                GameObject tile = Instantiate(tileToInstantiate, tilePosition, Quaternion.Euler(0, 30, 0));

                // Scale mountains based on height
                if (_heightMap[i, j] > _mountainThreshold)
                {
                    float scaleFactor = 1f + (_mountainHeight[i, j] - 1) * 0.25f;
                    tile.transform.localScale = new Vector3(1, scaleFactor, 1);
                }

                // Parent to map holder
                if (mapHolder != null)
                    tile.transform.parent = mapHolder;

                // Tag the tile according to its type
                AssignTag(tile, tileToInstantiate);
                _grid[i, j] = tile;

                // Assign identifier
                Vector3 customPosition = GetCustomWorldCoordinates(i, j);
                tile.name = $"Hex_{i}_{j}_X{customPosition.x:F2}_Z{customPosition.z:F2}";

                // Create and store the hex
                string tileType = tile.tag;
                Hex newHex = new Hex(i, j, tilePosition, tileType, tile);
                _hexGrid[i, j] = newHex;

                // Update neighbors for this hex
                UpdateNeighborsForHex(i, j);
            }
        }
    }

    void PlaceSpecialTiles()
    {
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                // Try to place a special tile with the defined rarity levels
                string tileTag = _grid[i, j].tag;
                float rand = (float)_prng.NextDouble();
                
                // Water tiles can only have lily pads
                if (tileTag == "Water")
                {
                    if (rand < _lilyPadSpawnChance && CanPlaceSpecialTile(i, j, _lilyPadPositions))
                    {
                        PlaceSpecialTileAt(i, j, lilyPadPrefab, 1, _lilyPadPositions); // Common rarity
                    }
                }
                // Land tiles can have chest, archery zone, or pod
                else if (tileTag == "Land" || tileTag == "Forest")
                {
                    if (rand < _chestSpawnChance && CanPlaceSpecialTile(i, j, _chestPositions))
                    {
                        PlaceSpecialTileAt(i, j, chestPrefab, 4, _chestPositions); // Very Rare
                    }
                    else if (rand < _archerySpawnChance && CanPlaceSpecialTile(i, j, _archeryPositions))
                    {
                        PlaceSpecialTileAt(i, j, archeryZonePrefab, 3, _archeryPositions); // Rare
                    }
                    else if (rand < _podSpawnChance && CanPlaceSpecialTile(i, j, _podPositions))
                    {
                        PlaceSpecialTileAt(i, j, podPrefab, 2, _podPositions); // Uncommon
                    }
                }
            }
        }
    }

    bool CanPlaceSpecialTile(int x, int y, List<Vector2Int> existingPositions)
    {
        // Check minimum distance from same type
        foreach (var pos in existingPositions)
        {
            int distance = HexDistance(new Vector2Int(x, y), pos);
            if (distance < minDistanceBetweenSameType)
                return false;
        }
        return true;
    }

    void PlaceSpecialTileAt(int x, int y, GameObject prefab, int rarityLevel, List<Vector2Int> positionList)
    {
        // Get the existing tile's properties
        Vector3 position = _hexGrid[x, y].WorldPosition;
        GameObject existingTile = _grid[x, y];
        string baseType = existingTile.tag;

        // Destroy the existing tile
        Destroy(existingTile);

        // Instantiate the special tile
        GameObject specialTile = Instantiate(prefab, position, Quaternion.Euler(0, 30, 0));

        if (mapHolder != null)
            specialTile.transform.parent = mapHolder;

        specialTile.tag = "SpecialTile";

        // Add visual indicator for rarity
        GameObject rarityIndicator = new GameObject($"Rarity_{rarityLevel}");
        rarityIndicator.transform.parent = specialTile.transform;

        // Store tile in the grid
        _grid[x, y] = specialTile;

        // Update the hex data
        _hexGrid[x, y].GameObject = specialTile;
        _hexGrid[x, y].Type = "SpecialTile";

        // Add a component to track the special tile data
        SpecialTileData tileData = specialTile.AddComponent<SpecialTileData>();
        tileData.baseType = baseType;
        tileData.rarityLevel = rarityLevel;
        tileData.specialType = prefab.name.Replace("(Clone)", "").Trim();

        // Record position
        positionList.Add(new Vector2Int(x, y));
    }

    // Utility methods needed by the class
    private Vector2 GetHexCords(int x, int y)
    {
        float xCord = x * tileSize * Mathf.Cos(Mathf.Deg2Rad * 30);
        float yCord = y * tileSize + ((x % 2 == 1) ? tileSize * 0.5f : 0);
        return new Vector2(xCord, yCord);
    }

    public static Vector3Int OffsetToCube(int col, int row)
    {
        var q = col;
        var r = row - (col + (col & 1)) / 2;
        return new Vector3Int(q, r, -q - r);
    }

    public Vector3 CubeToWorld(Vector3Int cube)
    {
        float x = HexXFull * (cube.x + cube.z/2.0f);
        float z = HexZFull * cube.z;
        return new Vector3(x, 0, z);
    }

    public Vector3 GetCustomWorldCoordinates(int x, int y)
    {
        Vector3Int cube = OffsetToCube(x, y);
        return CubeToWorld(cube);
    }

    void AssignTag(GameObject tile, GameObject prefab)
    {
        if (prefab == waterPrefab)
            tile.tag = "Water";
        else if (prefab == forestPrefab)
            tile.tag = "Forest";
        else if (prefab == mountainPrefab || prefab == mountainForestPrefab)
            tile.tag = "Mountain";
        else
            tile.tag = "Land";
    }

    Vector2Int[] GetHexDirections()
    {
        return new[]
        {
            new Vector2Int(0, 1),   // NE
            new Vector2Int(1, 0),   // E
            new Vector2Int(1, -1),  // SE
            new Vector2Int(0, -1),  // SW
            new Vector2Int(-1, 0),  // W
            new Vector2Int(-1, 1)   // NW
        };
    }

    void UpdateNeighborsForHex(int x, int y)
    {
        Hex currentHex = _hexGrid[x, y];
        Vector2Int[] directions = GetHexDirections();

        for (int i = 0; i < directions.Length; i++)
        {
            int neighborX = x + directions[i].x;
            int neighborY = y + directions[i].y;

            if (IsValidCoordinate(neighborX, neighborY) && _hexGrid[neighborX, neighborY] != null)
            {
                // Add neighbor to current hex
                currentHex.AddNeighbor((HexDirection)i, _hexGrid[neighborX, neighborY]);

                // Add current hex as neighbor to the neighbor
                _hexGrid[neighborX, neighborY].AddNeighbor(GetOppositeDirection((HexDirection)i), currentHex);
            }
        }
    }

    HexDirection GetOppositeDirection(HexDirection dir)
    {
        return (HexDirection)(((int)dir + 3) % 6);
    }

    bool IsValidCoordinate(int x, int y)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
    }

    int HexDistance(Vector2Int a, Vector2Int b)
    {
        Vector3Int cubeA = OffsetToCube(a.x, a.y);
        Vector3Int cubeB = OffsetToCube(b.x, b.y);

        return Mathf.Max(
            Mathf.Abs(cubeA.x - cubeB.x),
            Mathf.Abs(cubeA.y - cubeB.y),
            Mathf.Abs(cubeA.z - cubeB.z)
        );
    }

    public GameObject GetTileAt(int x, int y)
    {
        if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
        {
            return _grid[x, y];
        }
        return null;
    }
}

public class SpecialTileData : MonoBehaviour
{
    public string baseType;     // Original terrain type (Water, Land, etc)
    public int rarityLevel;     // 1-5 (Common to Legendary)
    public string specialType;  // Chest, ArcheryZone, etc.
}