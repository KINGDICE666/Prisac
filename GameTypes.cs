using Microsoft.Xna.Framework;

public struct Player
{
    public Player(Vector2 position, int health)
    {
        Position = position;
        Health = health;
        InvulnerableTimer = 0f;
        ShotCooldown = 0f;
    }

    public Vector2 Position;
    public int Health;
    public float InvulnerableTimer;
    public float ShotCooldown;
}

public struct Bullet
{
    public Bullet(Vector2 position, Vector2 velocity)
    {
        Position = position;
        Velocity = velocity;
    }

    public Vector2 Position;
    public Vector2 Velocity;
}

public struct Enemy
{
    public Enemy(Vector2 position, int health, float speed, EnemyType type = EnemyType.Fly)
    {
        Position = position;
        Health = health;
        Speed = speed;
        Velocity = Vector2.Zero;
        Type = type;
    }

    public Vector2 Position;
    public int Health;
    public float Speed;
    public Vector2 Velocity;
    public EnemyType Type;
}

public enum EnemyType
{
    Fly,
    Spider
}

public sealed class RoomData
{
    public RoomData(Point gridPosition)
    {
        GridPosition = gridPosition;
    }

    public Point GridPosition { get; }
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
