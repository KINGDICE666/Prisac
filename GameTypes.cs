using Microsoft.Xna.Framework;

public struct Player
{
    public Player(Vector2 position, int health)
    {
        Position = position;
        Health = health;
        InvulnerableTimer = 0f;
        ShotCooldown = 0f;
        Damage = 5f;
        FireRate = 5;
        Range = 5;
    }

    public Vector2 Position;
    public int Health;
    public float InvulnerableTimer;
    public float ShotCooldown;
    public float Damage;
    public int FireRate;
    public int Range;
}

public struct Bullet
{
    public Bullet(Vector2 position, Vector2 velocity, float damage, float maxDistance)
    {
        Position = position;
        Velocity = velocity;
        Damage = damage;
        MaxDistance = maxDistance;
        DistanceTravelled = 0f;
    }

    public Vector2 Position;
    public Vector2 Velocity;
    public float Damage;
    public float MaxDistance;
    public float DistanceTravelled;
}

public struct ShotEffect
{
    public ShotEffect(Vector2 position, Vector2 direction, float timer)
    {
        Position = position;
        Direction = direction;
        Timer = timer;
    }

    public Vector2 Position;
    public Vector2 Direction;
    public float Timer;
}

public struct Enemy
{
    public Enemy(Vector2 position, float health, float speed, EnemyType type = EnemyType.Fly)
    {
        Position = position;
        Health = health;
        Speed = speed;
        Velocity = Vector2.Zero;
        Type = type;
    }

    public Vector2 Position;
    public float Health;
    public float Speed;
    public Vector2 Velocity;
    public EnemyType Type;
}

public enum EnemyType
{
    Fly,
    Spider
}

public enum RoomType
{
    Normal,
    Item
}

public enum ItemType
{
    BrokenMakarov
}

public struct RoomItem
{
    public RoomItem(ItemType type, Vector2 position)
    {
        Type = type;
        Position = position;
    }

    public ItemType Type;
    public Vector2 Position;
}

public sealed class RoomData
{
    public RoomData(Point gridPosition, int templateId)
    {
        GridPosition = gridPosition;
        TemplateId = templateId;
    }

    public Point GridPosition { get; }
    public int TemplateId { get; }
    public RoomType Type { get; set; } = RoomType.Normal;
    public RoomItem? Item { get; set; }
    public List<RectangleF> Rocks { get; } = [];
    public List<Enemy> Enemies { get; } = [];
    public bool Cleared { get; set; }
}

public enum GameMenuState
{
    Playing,
    Pause,
    Settings
}

public enum ScreenMode
{
    Fullscreen,
    Windowed,
    Borderless
}
