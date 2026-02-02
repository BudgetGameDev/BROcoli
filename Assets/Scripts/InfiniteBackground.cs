using UnityEngine;

/// <summary>
/// Creates an infinite tiling background that follows the player using a 3x3 grid.
/// Attach to the existing background GameObject with a SpriteRenderer.
/// </summary>
public class InfiniteBackground : MonoBehaviour
{
    [SerializeField] private Transform _target;
    
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer[] _tiles;
    private Vector2 _tileSize;
    private const int GridSize = 3;
    
    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (_target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _target = player.transform;
        }
        
        if (_spriteRenderer != null && _spriteRenderer.sprite != null)
        {
            CreateTileGrid();
        }
    }
    
    private void CreateTileGrid()
    {
        var sprite = _spriteRenderer.sprite;
        _tileSize = new Vector2(
            sprite.bounds.size.x * transform.localScale.x,
            sprite.bounds.size.y * transform.localScale.y
        );
        
        _tiles = new SpriteRenderer[GridSize * GridSize];
        
        // Create 3x3 grid of tiles
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                int index = y * GridSize + x;
                
                if (x == 1 && y == 1)
                {
                    // Center tile is the original
                    _tiles[index] = _spriteRenderer;
                }
                else
                {
                    // Create clone tiles
                    var tileObj = new GameObject($"BackgroundTile_{x}_{y}");
                    tileObj.transform.SetParent(transform.parent);
                    tileObj.transform.localScale = transform.localScale;
                    
                    var tileSR = tileObj.AddComponent<SpriteRenderer>();
                    tileSR.sprite = _spriteRenderer.sprite;
                    tileSR.color = _spriteRenderer.color;
                    tileSR.sortingLayerID = _spriteRenderer.sortingLayerID;
                    tileSR.sortingOrder = _spriteRenderer.sortingOrder;
                    tileSR.material = _spriteRenderer.material;
                    
                    _tiles[index] = tileSR;
                }
            }
        }
    }
    
    private void LateUpdate()
    {
        if (_target == null || _tiles == null) return;
        
        // Calculate which tile the player is on
        Vector3 targetPos = _target.position;
        float centerX = Mathf.Floor(targetPos.x / _tileSize.x) * _tileSize.x;
        float centerY = Mathf.Floor(targetPos.y / _tileSize.y) * _tileSize.y;
        
        // Position all tiles in 3x3 grid around player
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                int index = y * GridSize + x;
                float tileX = centerX + (x - 1) * _tileSize.x;
                float tileY = centerY + (y - 1) * _tileSize.y;
                
                _tiles[index].transform.position = new Vector3(
                    tileX,
                    tileY,
                    transform.position.z
                );
            }
        }
    }
    
    private void OnDestroy()
    {
        // Clean up created tiles (skip center which is original)
        if (_tiles == null) return;
        
        for (int i = 0; i < _tiles.Length; i++)
        {
            if (i != 4 && _tiles[i] != null) // 4 is center (1,1) position
            {
                Destroy(_tiles[i].gameObject);
            }
        }
    }
}
