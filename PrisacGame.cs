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
    private const float EnemyAcceleration = 7.5f;
    private const float EnemySeparationRadius = 52f;
    private const int FloorRoomCount = 5;

    private static readonly RectangleF Room = new(72, 64, 880, 544);
    private static readonly Point Up = new(0, -1);
    private static readonly Point Down = new(0, 1);
    private static readonly Point Left = new(-1, 0);
    private static readonly Point Right = new(1, 0);
    private static readonly Point[] Directions = [Up, Down, Left, Right];

    private readonly GraphicsDeviceManager graphics;
    private readonly List<Bullet> bullets = [];
    private readonly Dictionary<Point, RoomData> floorRooms = [];
    private readonly Random random = new();

    private SpriteBatch spriteBatch = null!;
    private Texture2D pixel = null!;
    private Texture2D flyTexture = null!;
    private Player player;
    private Point currentRoomPosition;
    private RoomData currentRoom = null!;

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

        ResetFloor();
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
            ResetFloor();
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

        if (currentRoom.Enemies.Count == 0)
        {
            currentRoom.Cleared = true;
        }

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

    private void ResetFloor()
    {
        player = new Player(new Vector2(Room.Center.X, Room.Center.Y), 6);
        GenerateFloor();
        EnterRoom(Point.Zero, player.Position);
    }

    private void GenerateFloor()
    {
        floorRooms.Clear();
        var start = Point.Zero;
        floorRooms[start] = new RoomData(start);
        var frontier = new List<Point> { start };

        while (floorRooms.Count < FloorRoomCount)
        {
            var from = frontier[random.Next(frontier.Count)];
            var direction = Directions[random.Next(Directions.Length)];
            var next = Add(from, direction);

            if (floorRooms.ContainsKey(next))
            {
                continue;
            }

            floorRooms[next] = new RoomData(next);
            frontier.Add(next);
        }

        foreach (var roomData in floorRooms.Values)
        {
            FillRoom(roomData);
        }

        floorRooms[start].Cleared = true;
        floorRooms[start].Enemies.Clear();
    }

    private void FillRoom(RoomData roomData)
    {
        roomData.Rocks.Clear();
        roomData.Enemies.Clear();

        var offset = Math.Abs(roomData.GridPosition.X * 37 + roomData.GridPosition.Y * 53);
        roomData.Rocks.Add(new RectangleF(276 + offset % 80, 252, 72, 72));
        roomData.Rocks.Add(new RectangleF(676 - offset % 70, 252 + offset % 52, 72, 72));
        roomData.Rocks.Add(new RectangleF(476, 384 - offset % 64, 72, 72));

        if (roomData.GridPosition == Point.Zero)
        {
            return;
        }

        var enemyCount = 2 + Math.Abs(roomData.GridPosition.X + roomData.GridPosition.Y) % 3;
        var spawnPoints = new[]
        {
            new Vector2(220, 180),
            new Vector2(804, 180),
            new Vector2(512, 514),
            new Vector2(780, 500),
            new Vector2(244, 496)
        };

        for (var index = 0; index < enemyCount; index++)
        {
            var point = spawnPoints[(index + offset) % spawnPoints.Length];
            roomData.Enemies.Add(new Enemy(point, 3 + index % 2, 62 + index * 10));
        }
    }

    private void EnterRoom(Point roomPosition, Vector2 playerPosition)
    {
        currentRoomPosition = roomPosition;
        currentRoom = floorRooms[currentRoomPosition];
        player.Position = playerPosition;
        bullets.Clear();
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

        if (TryChangeRoom(nextPosition))
        {
            return;
        }

        nextPosition.X = MathHelper.Clamp(nextPosition.X, Room.Left + PlayerRadius, Room.Right - PlayerRadius);
        nextPosition.Y = MathHelper.Clamp(nextPosition.Y, Room.Top + PlayerRadius, Room.Bottom - PlayerRadius);

        if (!CircleHitsAnyRock(nextPosition, PlayerRadius))
        {
            player.Position = nextPosition;
        }
    }

    private bool TryChangeRoom(Vector2 nextPosition)
    {
        if (!currentRoom.Cleared)
        {
            return false;
        }

        if (nextPosition.Y < Room.Top + PlayerRadius && IsInHorizontalDoor(player.Position) && HasNeighbor(Up))
        {
            EnterRoom(Add(currentRoomPosition, Up), new Vector2(Room.Center.X, Room.Bottom - PlayerRadius - 6));
            return true;
        }

        if (nextPosition.Y > Room.Bottom - PlayerRadius && IsInHorizontalDoor(player.Position) && HasNeighbor(Down))
        {
            EnterRoom(Add(currentRoomPosition, Down), new Vector2(Room.Center.X, Room.Top + PlayerRadius + 6));
            return true;
        }

        if (nextPosition.X < Room.Left + PlayerRadius && IsInVerticalDoor(player.Position) && HasNeighbor(Left))
        {
            EnterRoom(Add(currentRoomPosition, Left), new Vector2(Room.Right - PlayerRadius - 6, Room.Center.Y));
            return true;
        }

        if (nextPosition.X > Room.Right - PlayerRadius && IsInVerticalDoor(player.Position) && HasNeighbor(Right))
        {
            EnterRoom(Add(currentRoomPosition, Right), new Vector2(Room.Left + PlayerRadius + 6, Room.Center.Y));
            return true;
        }

        return false;
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

            for (var enemyIndex = currentRoom.Enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
            {
                var enemy = currentRoom.Enemies[enemyIndex];
                if (Vector2.DistanceSquared(bullet.Position, enemy.Position) <= Square(EnemyRadius + BulletRadius))
                {
                    enemy.Health--;
                    shouldRemove = true;

                    if (enemy.Health <= 0)
                    {
                        currentRoom.Enemies.RemoveAt(enemyIndex);
                    }
                    else
                    {
                        currentRoom.Enemies[enemyIndex] = enemy;
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
        for (var index = 0; index < currentRoom.Enemies.Count; index++)
        {
            var enemy = currentRoom.Enemies[index];
            MoveEnemy(index, ref enemy, dt);

            if (Vector2.DistanceSquared(player.Position, enemy.Position) <= Square(EnemyRadius + PlayerRadius) &&
                player.InvulnerableTimer <= 0f)
            {
                player.Health--;
                player.InvulnerableTimer = 0.85f;
            }

            currentRoom.Enemies[index] = enemy;
        }
    }

    private void MoveEnemy(int enemyIndex, ref Enemy enemy, float dt)
    {
        var desiredDirection = player.Position - enemy.Position;
        if (desiredDirection.LengthSquared() > 0f)
        {
            desiredDirection.Normalize();
        }

        desiredDirection += GetEnemySeparation(enemyIndex, enemy.Position) * 0.75f;
        if (desiredDirection.LengthSquared() > 0f)
        {
            desiredDirection.Normalize();
        }

        var desiredVelocity = desiredDirection * enemy.Speed;
        enemy.Velocity = Vector2.Lerp(enemy.Velocity, desiredVelocity, MathHelper.Clamp(dt * EnemyAcceleration, 0f, 1f));

        if (enemy.Velocity.LengthSquared() <= 0.01f)
        {
            return;
        }

        var movement = enemy.Velocity * dt;
        if (TryMoveEnemy(ref enemy, movement))
        {
            return;
        }

        var horizontal = new Vector2(movement.X, 0f);
        var vertical = new Vector2(0f, movement.Y);
        var horizontalFirst = Math.Abs(movement.X) > Math.Abs(movement.Y);

        if (horizontalFirst)
        {
            if (TryMoveEnemy(ref enemy, horizontal) || TryMoveEnemy(ref enemy, vertical))
            {
                enemy.Velocity *= 0.82f;
                return;
            }
        }
        else if (TryMoveEnemy(ref enemy, vertical) || TryMoveEnemy(ref enemy, horizontal))
        {
            enemy.Velocity *= 0.82f;
            return;
        }

        var tangentA = new Vector2(-desiredDirection.Y, desiredDirection.X) * enemy.Speed * dt;
        var tangentB = new Vector2(desiredDirection.Y, -desiredDirection.X) * enemy.Speed * dt;
        var tangent = Vector2.DistanceSquared(enemy.Position + tangentA, player.Position) <
            Vector2.DistanceSquared(enemy.Position + tangentB, player.Position) ? tangentA : tangentB;

        if (TryMoveEnemy(ref enemy, tangent))
        {
            enemy.Velocity = Vector2.Normalize(tangent) * enemy.Speed * 0.55f;
            return;
        }

        enemy.Velocity *= 0.25f;
    }

    private Vector2 GetEnemySeparation(int enemyIndex, Vector2 position)
    {
        var push = Vector2.Zero;

        for (var otherIndex = 0; otherIndex < currentRoom.Enemies.Count; otherIndex++)
        {
            if (otherIndex == enemyIndex)
            {
                continue;
            }

            var away = position - currentRoom.Enemies[otherIndex].Position;
            var distanceSquared = away.LengthSquared();
            if (distanceSquared <= 0.001f || distanceSquared > Square(EnemySeparationRadius))
            {
                continue;
            }

            var distance = MathF.Sqrt(distanceSquared);
            push += away / distance * (1f - distance / EnemySeparationRadius);
        }

        return push;
    }

    private bool TryMoveEnemy(ref Enemy enemy, Vector2 movement)
    {
        if (movement.LengthSquared() <= 0.001f)
        {
            return false;
        }

        var nextPosition = enemy.Position + movement;
        if (nextPosition.X < Room.Left + EnemyRadius ||
            nextPosition.X > Room.Right - EnemyRadius ||
            nextPosition.Y < Room.Top + EnemyRadius ||
            nextPosition.Y > Room.Bottom - EnemyRadius ||
            CircleHitsAnyRock(nextPosition, EnemyRadius))
        {
            return false;
        }

        enemy.Position = nextPosition;
        return true;
    }

    private void DrawGame()
    {
        FillRect(new RectangleF(Room.X - 28, Room.Y - 28, Room.Width + 56, Room.Height + 56), new Color(51, 36, 31));
        FillRect(Room, new Color(91, 66, 52));
        DrawFloorTiles();
        DrawDoors();

        foreach (var rock in currentRoom.Rocks)
        {
            FillRect(rock, new Color(111, 106, 96));
            FillRect(new RectangleF(rock.X + 8, rock.Y + 8, rock.Width - 16, rock.Height - 16), new Color(87, 82, 73));
        }

        foreach (var enemy in currentRoom.Enemies)
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
        DrawDoor(Up, new RectangleF(Room.Center.X - 40, Room.Top - 28, 80, 32));
        DrawDoor(Down, new RectangleF(Room.Center.X - 40, Room.Bottom - 4, 80, 32));
        DrawDoor(Left, new RectangleF(Room.Left - 28, Room.Center.Y - 40, 32, 80));
        DrawDoor(Right, new RectangleF(Room.Right - 4, Room.Center.Y - 40, 32, 80));
    }

    private void DrawDoor(Point direction, RectangleF bounds)
    {
        if (!HasNeighbor(direction))
        {
            return;
        }

        var doorColor = currentRoom.Cleared ? new Color(195, 146, 77) : new Color(42, 32, 28);
        FillRect(bounds, doorColor);
    }

    private void DrawHud()
    {
        for (var heart = 0; heart < 3; heart++)
        {
            var color = heart * 2 < player.Health ? new Color(200, 60, 75) : new Color(59, 37, 41);
            FillCircle(new Vector2(40 + heart * 28, 32), 9, color);
        }

        var clearedCount = floorRooms.Values.Count(room => room.Cleared);
        var status = currentRoom.Cleared ? $"ROOM {clearedCount}/{FloorRoomCount}" : $"ENEMIES: {currentRoom.Enemies.Count}";
        DrawBlockText(status, new Vector2(784, 20), 2, new Color(241, 228, 208));
        DrawMiniMap();
    }

    private void DrawMiniMap()
    {
        const int cell = 12;
        var origin = new Vector2(42, 58);

        foreach (var roomData in floorRooms.Values)
        {
            var x = origin.X + roomData.GridPosition.X * (cell + 4);
            var y = origin.Y + roomData.GridPosition.Y * (cell + 4);
            var color = roomData.Cleared ? new Color(195, 146, 77) : new Color(74, 62, 58);
            if (roomData.GridPosition == currentRoomPosition)
            {
                color = new Color(207, 232, 243);
            }

            FillRect(new RectangleF(x, y, cell, cell), color);
        }
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
        foreach (var rock in currentRoom.Rocks)
        {
            if (CircleHitsRect(center, radius, rock))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasNeighbor(Point direction)
    {
        return floorRooms.ContainsKey(Add(currentRoomPosition, direction));
    }

    private static bool IsInHorizontalDoor(Vector2 position)
    {
        return MathF.Abs(position.X - Room.Center.X) <= 48f;
    }

    private static bool IsInVerticalDoor(Vector2 position)
    {
        return MathF.Abs(position.Y - Room.Center.Y) <= 48f;
    }

    private static Point Add(Point a, Point b)
    {
        return new Point(a.X + b.X, a.Y + b.Y);
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
        ['/'] = ["001", "001", "010", "100", "100"],
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
