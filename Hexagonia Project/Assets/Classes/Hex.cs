using System.Collections.Generic;
using UnityEngine;

namespace Classes
{
    public enum HexDirection
    {
        NE, E, SE, SW, W, NW
    }

    public class Hex
    {
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public Vector3 WorldPosition { get; private set; }
        public string Type { get; set; }
        public GameObject GameObject { get; set; }
        public Dictionary<HexDirection, Hex> Neighbors { get; private set; }

        public Hex(int x, int y, Vector3 worldPos, string type, GameObject gameObject)
        {
            GridX = x;
            GridY = y;
            WorldPosition = worldPos;
            Type = type;
            GameObject = gameObject;
            Neighbors = new Dictionary<HexDirection, Hex>();
        }

        public void AddNeighbor(HexDirection direction, Hex neighbor)
        {
            if (!Neighbors.ContainsKey(direction))
            {
                Neighbors.Add(direction, neighbor);
            }
            else
            {
                Neighbors[direction] = neighbor;
            }
        }
    }
}