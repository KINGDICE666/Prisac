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
    public Enemy(Vector2 position, int health, float speed)
    {
        Position = position;
        Health = health;
        Speed = speed;
    }

    public Vector2 Position;
    public int Health;
    public float Speed;
}
