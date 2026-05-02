using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.IO;

public sealed class PrisacGame : Game
{
    private const int ScreenWidth = 1024;
    private const int ScreenHeight = 672;
    private const float PlayerRadius = 17f;
    private const float PlayerSpeed = 245f;
    private const float BulletSpeed = 520f;
    private const float BulletRadius = 5f;
    private const float EnemyRadius = 18f;

    private static readonly RectangleF Room = new(72, 64, 880, 544);

    private readonly GraphicsDeviceManager graphics;
    private readonly List<Bullet> bullets = [];
    private readonly List<Enemy> enemies = [];
    private readonly List<RectangleF> rocks = [];

    private SpriteBatch spriteBatch = null!;
    private Texture2D pixel = null!;
    private Texture2D flyTexture = null!;
    private Player player;
    private bool roomCleared;

    public PrisacGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ScreenWidth,
            PreferredBackBufferHeight = ScreenHeight,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Prisac";
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([Color.White]);
        using var flyStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Sprites", "Enemies", "fly.png"));
        flyTexture = Texture2D.FromStream(GraphicsDevice, flyStream);
        ResetRoom();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (keyboard.IsKeyDown(Keys.R))
        {
            ResetRoom();
        }

        if (player.Health <= 0)
        {
            base.Update(gameTime);
            return;
        }

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        player.InvulnerableTimer = MathF.Max(player.InvulnerableTimer - dt, 0f);
        player.ShotCooldown = MathF.Max(player.ShotCooldown - dt, 0f);

        MovePlayer(keyboard, dt);
        Shoot(keyboard);
        UpdateBullets(dt);
        UpdateEnemies(dt);

        roomCleared = enemies.Count == 0;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(24, 19, 19));

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawGame();
        spriteBatch.End();

        base.Draw(gameTime);
    }

    private void ResetRoom()
    {
        player = new Player(new Vector2(Room.Center.X, Room.Center.Y), 6);
        roomCleared = false;
        bullets.Clear();

        rocks.Clear();
        rocks.Add(new RectangleF(276, 252, 72, 72));
        rocks.Add(new RectangleF(676, 252, 72, 72));
        rocks.Add(new RectangleF(476, 384, 72, 72));

        enemies.Clear();
        enemies.Add(new Enemy(new Vector2(220, 180), 3, 72));
        enemies.Add(new Enemy(new Vector2(804, 180), 3, 72));
        enemies.Add(new Enemy(new Vector2(512, 514), 4, 58));
    }

    private void MovePlayer(KeyboardState keyboard, float dt)
    {
        var direction = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.A)) direction.X -= 1f;
        if (keyboard.IsKeyDown(Keys.D)) direction.X += 1f;
        if (keyboard.IsKeyDown(Keys.W)) direction.Y -= 1f;
        if (keyboard.IsKeyDown(Keys.S)) direction.Y += 1f;

        if (direction == Vector2.Zero)
        {
            return;
        }

        direction.Normalize();
        var nextPosition = player.Position + direction * PlayerSpeed * dt;
        nextPosition.X = MathHelper.Clamp(nextPosition.X, Room.Left + PlayerRadius, Room.Right - PlayerRadius);
        nextPosition.Y = MathHelper.Clamp(nextPosition.Y, Room.Top + PlayerRadius, Room.Bottom - PlayerRadius);

        if (!CircleHitsAnyRock(nextPosition, PlayerRadius))
        {
            player.Position = nextPosition;
        }
    }

    private void Shoot(KeyboardState keyboard)
    {
        if (player.ShotCooldown > 0f)
        {
            return;
        }

        var direction = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.Left)) direction.X -= 1f;
        if (keyboard.IsKeyDown(Keys.Right)) direction.X += 1f;
        if (keyboard.IsKeyDown(Keys.Up)) direction.Y -= 1f;
        if (keyboard.IsKeyDown(Keys.Down)) direction.Y += 1f;

        if (direction == Vector2.Zero)
        {
            return;
        }

        direction.Normalize();
        bullets.Add(new Bullet(player.Position + direction * 24f, direction * BulletSpeed));
        player.ShotCooldown = 0.18f;
    }

    private void UpdateBullets(float dt)
    {
        for (var bulletIndex = bullets.Count - 1; bulletIndex >= 0; bulletIndex--)
        {
            var bullet = bullets[bulletIndex];
            bullet.Position += bullet.Velocity * dt;
            bullets[bulletIndex] = bullet;

            var shouldRemove = !Room.Contains(bullet.Position) || CircleHitsAnyRock(bullet.Position, BulletRadius);

            for (var enemyIndex = enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
            {
                var enemy = enemies[enemyIndex];
                if (Vector2.DistanceSquared(bullet.Position, enemy.Position) <= Square(EnemyRadius + BulletRadius))
                {
                    enemy.Health--;
                    shouldRemove = true;

                    if (enemy.Health <= 0)
                    {
                        enemies.RemoveAt(enemyIndex);
                    }
                    else
                    {
                        enemies[enemyIndex] = enemy;
                    }

                    break;
                }
            }

            if (shouldRemove)
            {
                bullets.RemoveAt(bulletIndex);
            }
        }
    }

    private void UpdateEnemies(float dt)
    {
        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            var toPlayer = player.Position - enemy.Position;

            if (toPlayer.LengthSquared() > 0f)
            {
                toPlayer.Normalize();
                var nextPosition = enemy.Position + toPlayer * enemy.Speed * dt;
                if (!CircleHitsAnyRock(nextPosition, EnemyRadius))
                {
                    enemy.Position = nextPosition;
                }
            }

            if (Vector2.DistanceSquared(player.Position, enemy.Position) <= Square(EnemyRadius + PlayerRadius) &&
                player.InvulnerableTimer <= 0f)
            {
                player.Health--;
                player.InvulnerableTimer = 0.85f;
            }

            enemies[index] = enemy;
        }
    }

    private void DrawGame()
    {
        FillRect(new RectangleF(Room.X - 28, Room.Y - 28, Room.Width + 56, Room.Height + 56), new Color(51, 36, 31));
        FillRect(Room, new Color(91, 66, 52));
        DrawFloorTiles();
        DrawDoors();

        foreach (var rock in rocks)
        {
            FillRect(rock, new Color(111, 106, 96));
            FillRect(new RectangleF(rock.X + 8, rock.Y + 8, rock.Width - 16, rock.Height - 16), new Color(87, 82, 73));
        }

        foreach (var enemy in enemies)
        {
            DrawEnemy(enemy);
        }

        foreach (var bullet in bullets)
        {
            FillCircle(bullet.Position, BulletRadius, new Color(207, 232, 243));
        }

        var playerColor = new Color(242, 209, 179);
        if (player.InvulnerableTimer > 0f && (int)(player.InvulnerableTimer * 16f) % 2 == 0)
        {
            playerColor = new Color(247, 242, 231);
        }

        FillCircle(player.Position, PlayerRadius, playerColor);
        FillCircle(player.Position + new Vector2(-6, -4), 3, new Color(23, 17, 15));
        FillCircle(player.Position + new Vector2(6, -4), 3, new Color(23, 17, 15));
        DrawHud();

        if (player.Health <= 0)
        {
            FillRect(new RectangleF(0, 0, ScreenWidth, ScreenHeight), new Color(0, 0, 0, 140));
            DrawBlockText("YOU DIED - PRESS R", new Vector2(332, 304), 4, new Color(241, 228, 208));
        }
    }

    private void DrawFloorTiles()
    {
        for (var x = Room.Left; x <= Room.Right; x += 64)
        {
            FillRect(new RectangleF(x, Room.Top, 1, Room.Height), new Color(102, 75, 62));
        }

        for (var y = Room.Top; y <= Room.Bottom; y += 64)
        {
            FillRect(new RectangleF(Room.Left, y, Room.Width, 1), new Color(102, 75, 62));
        }
    }

    private void DrawDoors()
    {
        var doorColor = roomCleared ? new Color(195, 146, 77) : new Color(42, 32, 28);
        FillRect(new RectangleF(Room.Center.X - 40, Room.Top - 28, 80, 32), doorColor);
        FillRect(new RectangleF(Room.Center.X - 40, Room.Bottom - 4, 80, 32), doorColor);
        FillRect(new RectangleF(Room.Left - 28, Room.Center.Y - 40, 32, 80), doorColor);
        FillRect(new RectangleF(Room.Right - 4, Room.Center.Y - 40, 32, 80), doorColor);
    }

    private void DrawHud()
    {
        for (var heart = 0; heart < 3; heart++)
        {
            var color = heart * 2 < player.Health ? new Color(200, 60, 75) : new Color(59, 37, 41);
            FillCircle(new Vector2(40 + heart * 28, 32), 9, color);
        }

        DrawBlockText(roomCleared ? "ROOM CLEAR" : $"ENEMIES: {enemies.Count}", new Vector2(812, 20), 2, new Color(241, 228, 208));
    }

    private void DrawEnemy(Enemy enemy)
    {
        var size = EnemyRadius * 2.6f;
        var destination = new Rectangle(
            (int)(enemy.Position.X - size / 2f),
            (int)(enemy.Position.Y - size / 2f),
            (int)size,
            (int)size);

        spriteBatch.Draw(flyTexture, destination, Color.White);
    }

    private void FillRect(RectangleF rect, Color color)
    {
        spriteBatch.Draw(pixel, rect.ToRectangle(), color);
    }

    private void FillCircle(Vector2 center, float radius, Color color)
    {
        var diameter = (int)(radius * 2);
        var left = (int)(center.X - radius);
        var top = (int)(center.Y - radius);

        for (var y = 0; y < diameter; y++)
        {
            for (var x = 0; x < diameter; x++)
            {
                var dx = x - radius;
                var dy = y - radius;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    spriteBatch.Draw(pixel, new Rectangle(left + x, top + y, 1, 1), color);
                }
            }
        }
    }

    private void DrawBlockText(string text, Vector2 position, int scale, Color color)
    {
        var cursor = position;
        foreach (var character in text.ToUpperInvariant())
        {
            if (character == ' ')
            {
                cursor.X += 4 * scale;
                continue;
            }

            if (Glyphs.TryGetValue(character, out var rows))
            {
                for (var row = 0; row < rows.Length; row++)
                {
                    for (var column = 0; column < rows[row].Length; column++)
                    {
                        if (rows[row][column] == '1')
                        {
                            FillRect(new RectangleF(cursor.X + column * scale, cursor.Y + row * scale, scale, scale), color);
                        }
                    }
                }
            }

            cursor.X += 6 * scale;
        }
    }

    private bool CircleHitsAnyRock(Vector2 center, float radius)
    {
        foreach (var rock in rocks)
        {
            if (CircleHitsRect(center, radius, rock))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CircleHitsRect(Vector2 center, float radius, RectangleF rect)
    {
        var closestX = MathHelper.Clamp(center.X, rect.Left, rect.Right);
        var closestY = MathHelper.Clamp(center.Y, rect.Top, rect.Bottom);
        var dx = center.X - closestX;
        var dy = center.Y - closestY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static float Square(float value) => value * value;

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['0'] = ["111", "101", "101", "101", "111"],
        ['1'] = ["010", "110", "010", "010", "111"],
        ['2'] = ["111", "001", "111", "100", "111"],
        ['3'] = ["111", "001", "111", "001", "111"],
        ['4'] = ["101", "101", "111", "001", "001"],
        ['5'] = ["111", "100", "111", "001", "111"],
        ['6'] = ["111", "100", "111", "101", "111"],
        ['7'] = ["111", "001", "010", "010", "010"],
        ['8'] = ["111", "101", "111", "101", "111"],
        ['9'] = ["111", "101", "111", "001", "111"],
        [':'] = ["0", "1", "0", "1", "0"],
        ['-'] = ["000", "000", "111", "000", "000"],
        ['A'] = ["111", "101", "111", "101", "101"],
        ['C'] = ["111", "100", "100", "100", "111"],
        ['D'] = ["110", "101", "101", "101", "110"],
        ['E'] = ["111", "100", "111", "100", "111"],
        ['I'] = ["111", "010", "010", "010", "111"],
        ['L'] = ["100", "100", "100", "100", "111"],
        ['M'] = ["101", "111", "111", "101", "101"],
        ['N'] = ["101", "111", "111", "111", "101"],
        ['O'] = ["111", "101", "101", "101", "111"],
        ['P'] = ["111", "101", "111", "100", "100"],
        ['R'] = ["110", "101", "110", "101", "101"],
        ['S'] = ["111", "100", "111", "001", "111"],
        ['T'] = ["111", "010", "010", "010", "010"],
        ['U'] = ["101", "101", "101", "101", "111"],
        ['Y'] = ["101", "101", "010", "010", "010"],
    };
}
