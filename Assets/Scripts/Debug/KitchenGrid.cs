using System.Collections.Generic;
using UnityEngine;

// The kitchen floor as a grid of whole-tile cells (x from -10 to 9, y from -4 to -1),
// with breadth-first path finding. Used by the test bots to walk Enzo around.
static public class KitchenGrid {
    public const int MIN_X = -10, MAX_X = 9, MIN_Y = -4, MAX_Y = -1;

    static public Vector2Int Cell( Vector2 p ) {
        return new Vector2Int( Mathf.FloorToInt( p.x ), Mathf.FloorToInt( p.y ) );
    }

    static public Vector2 CellCenter( Vector2Int c ) {
        return new Vector2( c.x + 0.5f, c.y + 0.5f );
    }

    static public bool Walkable( Vector2Int c ) {
        if ( c.x < MIN_X || c.x > MAX_X || c.y < MIN_Y || c.y > MAX_Y ) return false;
        // The island counter in the middle of the kitchen, and the plants in the top corners
        if ( c.y == -2 && c.x >= -2 && c.x <= 1 ) return false;
        if ( c.y == -1 && ( c.x == MIN_X || c.x == MAX_X ) ) return false;
        return true;
    }

    // Breadth-first search to any walkable cell next to the target's cell
    static public List<Vector2Int> FindPath( Vector2Int start, Interactable target ) {
        Vector2Int goalCell = Cell( target.Center );
        HashSet<Vector2Int> goals = new HashSet<Vector2Int>();
        foreach ( Vector2Int d in DIRS ) if ( Walkable( goalCell + d ) ) goals.Add( goalCell + d );
        if ( !Walkable( start ) ) start = NearestWalkable( start );

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue( start );
        cameFrom[start] = start;
        while ( open.Count > 0 ) {
            Vector2Int c = open.Dequeue();
            if ( goals.Contains( c ) ) {
                List<Vector2Int> result = new List<Vector2Int>();
                while ( c != start ) {
                    result.Insert( 0, c );
                    c = cameFrom[c];
                }
                result.Insert( 0, start );
                return result;
            }
            foreach ( Vector2Int d in DIRS ) {
                Vector2Int n = c + d;
                if ( Walkable( n ) && !cameFrom.ContainsKey( n ) ) {
                    cameFrom[n] = c;
                    open.Enqueue( n );
                }
            }
        }
        return new List<Vector2Int>();
    }

    static public Vector2Int NearestWalkable( Vector2Int c ) {
        c.x = Mathf.Clamp( c.x, MIN_X, MAX_X );
        c.y = Mathf.Clamp( c.y, MIN_Y, MAX_Y );
        if ( !Walkable( c ) ) c.y = -1;
        return c;
    }

    static private readonly Vector2Int[] DIRS = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
}
