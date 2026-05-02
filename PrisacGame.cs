using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.IO;

public sealed class PrisacGame : Game
{
    private const int DefaultBackBufferWidth = 1920;
    private const int DefaultBackBufferHeight = 1080;
    private const int ScreenWidth = 1920;
    private const int ScreenHeight = 1080;
    private const float PlayerRadius = 28f;
    private const float PlayerSpeed = 315f;
    private const float BulletSpeed = 640f;
    private const float BulletRadius = 8f;
    private const float ShotCooldown = 0.32f;
    private const float ShotEffectDuration = 0.13f;
    private const float EnemyRadius = 29f;
    private const float EnemyAcceleration = 7.5f;
    private const float EnemySeparationRadius = 84f;
    private const float PlayerContactInvulnerability = 0.22f;
    private const int FloorRoomCount = 5;

    private static readonly RectangleF Room = new(135, 100, 1650, 880);
    private static readonly Point Up = new(0, -1);
    private static readonly Point Down = new(0, 1);
    private static readonly Point Left = new(-1, 0);
    private static readonly Point Right = new(1, 0);
    private static readonly Point[] Directions = [Up, Down, Left, Right];
    private static readonly Point[] Resolutions =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080)
    ];

    private readonly GraphicsDeviceManager graphics;
    private readonly List<Bullet> bullets = [];
    private readonly List<ShotEffect> shotEffects = [];
    private readonly Dictionary<Point, RoomData> floorRooms = [];
    private readonly Random random = new();

    private GameMenuState menuState = GameMenuState.Playing;
    private KeyboardState previousKeyboard;
    private MouseState previousMouse;
    private int selectedMenuItem;
    private int selectedSettingsItem;
    private int resolutionIndex = 2;
    private ScreenMode screenMode = ScreenMode.Fullscreen;

    private SpriteBatch spriteBatch = null!;
    private Texture2D pixel = null!;
    private Texture2D flyTexture = null!;
    private Texture2D spiderTexture = null!;
    private Player player;
    private Point currentRoomPosition;
    private RoomData currentRoom = null!;

    public PrisacGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = DefaultBackBufferWidth,
            PreferredBackBufferHeight = DefaultBackBufferHeight,
            IsFullScreen = true,
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

        using var flyStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Sprites", "Enemies", "fly_directions.png"));
        flyTexture = Texture2D.FromStream(GraphicsDevice, flyStream);
        using var spiderStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Sprites", "Enemies", "spider_directions.png"));
        spiderTexture = Texture2D.FromStream(GraphicsDevice, spiderStream);

        ResetFloor();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            ToggleMenuBack();
        }

        if (menuState != GameMenuState.Playing)
        {
            UpdateMenu(keyboard, mouse);
            previousKeyboard = keyboard;
            previousMouse = mouse;
            base.Update(gameTime);
            return;
        }

        if (keyboard.IsKeyDown(Keys.R))
        {
            ResetFloor();
        }

        if (player.Health <= 0)
        {
            previousKeyboard = keyboard;
            previousMouse = mouse;
            base.Update(gameTime);
            return;
        }

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        player.InvulnerableTimer = MathF.Max(player.InvulnerableTimer - dt, 0f);
        player.ShotCooldown = MathF.Max(player.ShotCooldown - dt, 0f);

        MovePlayer(keyboard, dt);
        Shoot(keyboard);
        UpdateBullets(dt);
        UpdateShotEffects(dt);
        UpdateEnemies(dt);

        if (currentRoom.Enemies.Count == 0)
        {
            currentRoom.Cleared = true;
        }

        previousKeyboard = keyboard;
        previousMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(24, 19, 19));

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawGame();
        DrawMenu();
        spriteBatch.End();

        base.Draw(gameTime);
    }

    private void ToggleMenuBack()
    {
        if (menuState == GameMenuState.Playing)
        {
            menuState = GameMenuState.Pause;
            selectedMenuItem = 0;
        }
        else if (menuState == GameMenuState.Settings)
        {
            menuState = GameMenuState.Pause;
        }
        else
        {
            menuState = GameMenuState.Playing;
        }
    }

    private void UpdateMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (menuState == GameMenuState.Pause)
        {
            UpdatePauseMenu(keyboard, mouse);
            return;
        }

        UpdateSettingsMenu(keyboard, mouse);
    }

    private void UpdatePauseMenu(KeyboardState keyboard, MouseState mouse)
    {
        var buttons = GetPauseButtons();
        if (IsKeyPressed(keyboard, Keys.Up) || IsKeyPressed(keyboard, Keys.W))
        {
            selectedMenuItem = Wrap(selectedMenuItem - 1, buttons.Length);
        }

        if (IsKeyPressed(keyboard, Keys.Down) || IsKeyPressed(keyboard, Keys.S))
        {
            selectedMenuItem = Wrap(selectedMenuItem + 1, buttons.Length);
        }

        var clickedButton = GetClickedButton(mouse, buttons);
        if (clickedButton >= 0)
        {
            selectedMenuItem = clickedButton;
        }

        if (IsKeyPressed(keyboard, Keys.Enter) || IsKeyPressed(keyboard, Keys.Space) || clickedButton >= 0)
        {
            if (selectedMenuItem == 0)
            {
                menuState = GameMenuState.Playing;
            }
            else if (selectedMenuItem == 1)
            {
                menuState = GameMenuState.Settings;
                selectedSettingsItem = 0;
            }
            else
            {
                Exit();
            }
        }
    }

    private void UpdateSettingsMenu(KeyboardState keyboard, MouseState mouse)
    {
        var buttons = GetSettingsButtons();
        if (IsKeyPressed(keyboard, Keys.Up) || IsKeyPressed(keyboard, Keys.W))
        {
            selectedSettingsItem = Wrap(selectedSettingsItem - 1, buttons.Length);
        }

        if (IsKeyPressed(keyboard, Keys.Down) || IsKeyPressed(keyboard, Keys.S))
        {
            selectedSettingsItem = Wrap(selectedSettingsItem + 1, buttons.Length);
        }

        var clickedButton = GetClickedButton(mouse, buttons);
        if (clickedButton >= 0)
        {
            selectedSettingsItem = clickedButton;
        }

        if (IsKeyPressed(keyboard, Keys.Left) || IsKeyPressed(keyboard, Keys.A))
        {
            ChangeSetting(-1);
        }

        if (IsKeyPressed(keyboard, Keys.Right) || IsKeyPressed(keyboard, Keys.D))
        {
            ChangeSetting(1);
        }

        if (IsKeyPressed(keyboard, Keys.Enter) || IsKeyPressed(keyboard, Keys.Space) || clickedButton >= 0)
        {
            if (selectedSettingsItem == 2)
            {
                menuState = GameMenuState.Pause;
            }
            else
            {
                ChangeSetting(1);
            }
        }
    }

    private void ChangeSetting(int direction)
    {
        if (selectedSettingsItem == 0)
        {
            screenMode = (ScreenMode)Wrap((int)screenMode + direction, 3);
            ApplyScreenSettings();
        }
        else if (selectedSettingsItem == 1)
        {
            resolutionIndex = Wrap(resolutionIndex + direction, Resolutions.Length);
            ApplyScreenSettings();
        }
    }

    private void ApplyScreenSettings()
    {
        var resolution = Resolutions[resolutionIndex];
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        Window.IsBorderless = screenMode == ScreenMode.Borderless;
        graphics.IsFullScreen = screenMode == ScreenMode.Fullscreen;
        graphics.PreferredBackBufferWidth = screenMode == ScreenMode.Borderless ? displayMode.Width : resolution.X;
        graphics.PreferredBackBufferHeight = screenMode == ScreenMode.Borderless ? displayMode.Height : resolution.Y;
        graphics.ApplyChanges();
    }

    private void DrawMenu()
    {
        if (menuState == GameMenuState.Playing)
        {
            return;
        }

        FillRect(new RectangleF(0, 0, ScreenWidth, ScreenHeight), new Color(0, 0, 0, 150));

        if (menuState == GameMenuState.Pause)
        {
            DrawPauseMenu();
        }
        else
        {
            DrawSettingsMenu();
        }
    }

    private void DrawPauseMenu()
    {
        FillRect(new RectangleF(660, 190, 600, 640), new Color(36, 27, 25, 235));
        FillRect(new RectangleF(680, 210, 560, 600), new Color(72, 52, 43, 235));
        DrawBlockText("МЕНЮ", new Vector2(850, 260), 7, new Color(241, 228, 208));

        var buttons = GetPauseButtons();
        DrawButton(buttons[0], "ПРОДОЛЖИТЬ", selectedMenuItem == 0);
        DrawButton(buttons[1], "НАСТРОЙКИ", selectedMenuItem == 1);
        DrawButton(buttons[2], "ВЫХОД", selectedMenuItem == 2);
    }

    private void DrawSettingsMenu()
    {
        FillRect(new RectangleF(510, 150, 900, 760), new Color(36, 27, 25, 235));
        FillRect(new RectangleF(530, 170, 860, 720), new Color(72, 52, 43, 235));
        DrawBlockText("НАСТРОЙКИ", new Vector2(690, 240), 7, new Color(241, 228, 208));

        var buttons = GetSettingsButtons();
        DrawButton(buttons[0], $"ЭКРАН: {GetScreenModeText()}", selectedSettingsItem == 0);
        DrawButton(buttons[1], $"РАЗРЕШЕНИЕ: {GetResolutionText()}", selectedSettingsItem == 1);
        DrawButton(buttons[2], "НАЗАД", selectedSettingsItem == 2);
    }

    private void DrawButton(RectangleF rect, string text, bool selected)
    {
        var border = selected ? new Color(233, 177, 88) : new Color(103, 78, 63);
        var fill = selected ? new Color(98, 70, 51) : new Color(51, 38, 34);
        var scale = text.Length > 16 ? 4 : 5;
        var textY = rect.Y + (rect.Height - 5 * scale) / 2f;
        FillRect(rect, border);
        FillRect(new RectangleF(rect.X + 6, rect.Y + 6, rect.Width - 12, rect.Height - 12), fill);
        DrawBlockText(text, new Vector2(rect.X + 28, textY), scale, new Color(241, 228, 208));
    }

    private RectangleF[] GetPauseButtons() =>
    [
        new(760, 410, 400, 90),
        new(760, 540, 400, 90),
        new(760, 670, 400, 90)
    ];

    private RectangleF[] GetSettingsButtons() =>
    [
        new(610, 390, 700, 90),
        new(610, 530, 700, 90),
        new(610, 740, 700, 90)
    ];

    private string GetScreenModeText() => screenMode switch
    {
        ScreenMode.Windowed => "В ОКНЕ",
        ScreenMode.Borderless => "ВЕСЬ ЭКРАН",
        _ => "ПОЛНЫЙ"
    };

    private string GetResolutionText()
    {
        var resolution = Resolutions[resolutionIndex];
        return $"{resolution.X}X{resolution.Y}";
    }

    private int GetClickedButton(MouseState mouse, RectangleF[] buttons)
    {
        if (mouse.LeftButton != ButtonState.Pressed || previousMouse.LeftButton == ButtonState.Pressed)
        {
            return -1;
        }

        var point = new Vector2(mouse.X, mouse.Y);

        for (var index = 0; index < buttons.Length; index++)
        {
            if (buttons[index].Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    private bool IsKeyPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !previousKeyboard.IsKeyDown(key);

    private static int Wrap(int value, int length) => (value % length + length) % length;

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
        floorRooms[start] = new RoomData(start, random.Next(6));
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

            floorRooms[next] = new RoomData(next, PickTemplateForRoom(next));
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

        var offset = Math.Abs(roomData.GridPosition.X * 37 + roomData.GridPosition.Y * 53 + roomData.TemplateId * 29);
        var template = roomData.TemplateId;
        var spawnPoints = ApplyRoomTemplate(roomData, template);
        RemoveUnsafeRocks(roomData);

        if (roomData.GridPosition == Point.Zero)
        {
            return;
        }

        var enemyCount = random.Next(2, 6);

        for (var index = 0; index < enemyCount; index++)
        {
            var point = spawnPoints[(index + random.Next(spawnPoints.Length)) % spawnPoints.Length];
            var type = ChooseEnemyType();
            var health = type == EnemyType.Spider ? 5 : random.Next(3, 5);
            var speed = type == EnemyType.Spider ? random.Next(88, 112) : random.Next(62, 92);
            roomData.Enemies.Add(new Enemy(point, health, speed, type));
        }
    }

    private int PickTemplateForRoom(Point roomPosition)
    {
        var usedNeighborTemplates = new HashSet<int>();
        foreach (var direction in Directions)
        {
            if (floorRooms.TryGetValue(Add(roomPosition, direction), out var neighbor))
            {
                usedNeighborTemplates.Add(neighbor.TemplateId);
            }
        }

        var choices = Enumerable.Range(0, 6)
            .Where(template => !usedNeighborTemplates.Contains(template))
            .ToArray();

        if (choices.Length == 0)
        {
            choices = Enumerable.Range(0, 6).ToArray();
        }

        return choices[random.Next(choices.Length)];
    }

    private static Vector2[] ApplyRoomTemplate(RoomData roomData, int template)
    {
        var tile = 116f;
        var left = Room.X + 260;
        var right = Room.Right - 376;
        var top = Room.Y + 185;
        var bottom = Room.Bottom - 301;
        var centerX = Room.Center.X - tile / 2f;
        var centerY = Room.Center.Y - tile / 2f;

        switch (template)
        {
            case 0:
                roomData.Rocks.Add(new RectangleF(Room.Center.X - 340, Room.Center.Y - 250, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X + 224, Room.Center.Y - 250, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X - 340, Room.Center.Y + 134, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X + 224, Room.Center.Y + 134, tile, tile));
                return
                [
                    new Vector2(Room.X + 290, Room.Y + 220),
                    new Vector2(Room.Right - 290, Room.Y + 220),
                    new Vector2(Room.X + 290, Room.Bottom - 220),
                    new Vector2(Room.Right - 290, Room.Bottom - 220)
                ];

            case 1:
                roomData.Rocks.Add(new RectangleF(Room.Center.X - 340, Room.Y + 250, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X - 340, Room.Y + 430, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X + 224, Room.Y + 250, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X + 224, Room.Y + 430, tile, tile));
                return
                [
                    new Vector2(Room.Center.X, Room.Y + 190),
                    new Vector2(Room.Center.X, Room.Bottom - 190),
                    new Vector2(Room.X + 260, Room.Center.Y),
                    new Vector2(Room.Right - 260, Room.Center.Y)
                ];

            case 2:
                roomData.Rocks.Add(new RectangleF(centerX - 250, centerY - 135, tile, tile));
                roomData.Rocks.Add(new RectangleF(centerX + 134, centerY - 135, tile, tile));
                roomData.Rocks.Add(new RectangleF(centerX - 250, centerY + 135, tile, tile));
                roomData.Rocks.Add(new RectangleF(centerX + 134, centerY + 135, tile, tile));
                return
                [
                    new Vector2(Room.X + 300, Room.Y + 210),
                    new Vector2(Room.Right - 300, Room.Y + 210),
                    new Vector2(Room.Center.X, Room.Bottom - 190),
                    new Vector2(Room.X + 330, Room.Bottom - 240),
                    new Vector2(Room.Right - 330, Room.Bottom - 240)
                ];

            case 3:
                roomData.Rocks.Add(new RectangleF(left, top, tile, tile));
                roomData.Rocks.Add(new RectangleF(left + 210, top + 160, tile, tile));
                roomData.Rocks.Add(new RectangleF(left + 420, top + 320, tile, tile));
                roomData.Rocks.Add(new RectangleF(left + 630, top + 480, tile, tile));
                return
                [
                    new Vector2(Room.Right - 300, Room.Y + 200),
                    new Vector2(Room.X + 300, Room.Bottom - 200),
                    new Vector2(Room.Center.X, Room.Y + 225),
                    new Vector2(Room.Center.X, Room.Bottom - 225)
                ];

            case 4:
                roomData.Rocks.Add(new RectangleF(left, top, tile, tile));
                roomData.Rocks.Add(new RectangleF(right, top, tile, tile));
                roomData.Rocks.Add(new RectangleF(left, bottom, tile, tile));
                roomData.Rocks.Add(new RectangleF(right, bottom, tile, tile));
                return
                [
                    new Vector2(Room.Center.X - 220, Room.Center.Y),
                    new Vector2(Room.Center.X + 220, Room.Center.Y),
                    new Vector2(Room.Center.X, Room.Center.Y - 180),
                    new Vector2(Room.Center.X, Room.Center.Y + 180)
                ];

            default:
                roomData.Rocks.Add(new RectangleF(Room.Center.X - 480, Room.Center.Y - 190, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X - 250, Room.Center.Y - 50, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X - 20, Room.Center.Y + 90, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X + 210, Room.Center.Y - 50, tile, tile));
                roomData.Rocks.Add(new RectangleF(Room.Center.X + 440, Room.Center.Y + 90, tile, tile));
                return
                [
                    new Vector2(Room.X + 280, Room.Y + 210),
                    new Vector2(Room.Right - 280, Room.Y + 210),
                    new Vector2(Room.X + 280, Room.Bottom - 210),
                    new Vector2(Room.Right - 280, Room.Bottom - 210),
                    new Vector2(Room.Center.X, Room.Center.Y)
                ];
        }
    }

    private static void RemoveUnsafeRocks(RoomData roomData)
    {
        var safeZones = new[]
        {
            new RectangleF(Room.Center.X - 150, Room.Center.Y - 150, 300, 300),
            new RectangleF(Room.Center.X - 115, Room.Top, 230, 185),
            new RectangleF(Room.Center.X - 115, Room.Bottom - 185, 230, 185),
            new RectangleF(Room.Left, Room.Center.Y - 115, 185, 230),
            new RectangleF(Room.Right - 185, Room.Center.Y - 115, 185, 230)
        };

        roomData.Rocks.RemoveAll(rock => safeZones.Any(zone => rock.Intersects(zone)));
    }

    private EnemyType ChooseEnemyType()
    {
        return random.NextDouble() < 0.35 ? EnemyType.Spider : EnemyType.Fly;
    }

    private void EnterRoom(Point roomPosition, Vector2 playerPosition)
    {
        currentRoomPosition = roomPosition;
        currentRoom = floorRooms[currentRoomPosition];
        player.Position = playerPosition;
        bullets.Clear();
        shotEffects.Clear();
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

        var direction = GetShootDirection(keyboard);

        if (direction == Vector2.Zero)
        {
            return;
        }

        bullets.Add(new Bullet(player.Position + direction * 40f, direction * BulletSpeed));
        shotEffects.Add(new ShotEffect(player.Position + direction * 38f, direction, ShotEffectDuration));
        player.ShotCooldown = ShotCooldown;
    }

    private static Vector2 GetShootDirection(KeyboardState keyboard)
    {
        if (keyboard.IsKeyDown(Keys.Left))
        {
            return new Vector2(-1f, 0f);
        }

        if (keyboard.IsKeyDown(Keys.Right))
        {
            return new Vector2(1f, 0f);
        }

        if (keyboard.IsKeyDown(Keys.Up))
        {
            return new Vector2(0f, -1f);
        }

        if (keyboard.IsKeyDown(Keys.Down))
        {
            return new Vector2(0f, 1f);
        }

        return Vector2.Zero;
    }

    private void UpdateShotEffects(float dt)
    {
        for (var index = shotEffects.Count - 1; index >= 0; index--)
        {
            var effect = shotEffects[index];
            effect.Timer -= dt;

            if (effect.Timer <= 0f)
            {
                shotEffects.RemoveAt(index);
            }
            else
            {
                shotEffects[index] = effect;
            }
        }
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
                player.InvulnerableTimer = PlayerContactInvulnerability;
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
        FillRect(new RectangleF(Room.X - 48, Room.Y - 48, Room.Width + 96, Room.Height + 96), new Color(51, 36, 31));
        FillRect(Room, new Color(91, 66, 52));
        DrawFloorTiles();
        DrawDoors();

        foreach (var rock in currentRoom.Rocks)
        {
            FillRect(rock, new Color(111, 106, 96));
            FillRect(new RectangleF(rock.X + 13, rock.Y + 13, rock.Width - 26, rock.Height - 26), new Color(87, 82, 73));
        }

        foreach (var enemy in currentRoom.Enemies)
        {
            DrawEnemy(enemy);
        }

        DrawShotEffects();

        foreach (var bullet in bullets)
        {
            DrawBulletTrail(bullet);
            DrawTearBullet(bullet);
        }

        var playerColor = new Color(242, 209, 179);
        if (player.InvulnerableTimer > 0f && (int)(player.InvulnerableTimer * 16f) % 2 == 0)
        {
            playerColor = new Color(247, 242, 231);
        }

        FillCircle(player.Position, PlayerRadius, playerColor);
        FillCircle(player.Position + new Vector2(-10, -7), 5, new Color(23, 17, 15));
        FillCircle(player.Position + new Vector2(10, -7), 5, new Color(23, 17, 15));
        DrawHud();

        if (player.Health <= 0)
        {
            FillRect(new RectangleF(0, 0, ScreenWidth, ScreenHeight), new Color(0, 0, 0, 140));
            DrawBlockText("YOU DIED - PRESS R", new Vector2(590, 500), 7, new Color(241, 228, 208));
        }
    }

    private void DrawShotEffects()
    {
        foreach (var effect in shotEffects)
        {
            var fade = MathHelper.Clamp(effect.Timer / ShotEffectDuration, 0f, 1f);
            var pop = 1f - fade;
            var center = effect.Position + effect.Direction * (20f * pop);
            var side = new Vector2(-effect.Direction.Y, effect.Direction.X);
            var splashAlpha = (int)(170 * fade);
            var shineAlpha = (int)(210 * fade);

            FillCircle(center - effect.Direction * 8f, 14f * fade, new Color(116, 177, 211, splashAlpha));
            FillCircle(center, 10f + 7f * fade, new Color(184, 226, 244, splashAlpha));
            FillCircle(center - effect.Direction * 4f - side * 4f, 4f + 3f * fade, new Color(247, 252, 255, shineAlpha));
            FillCircle(center - effect.Direction * 18f, 5f * fade, new Color(88, 139, 176, (int)(95 * fade)));
        }
    }

    private void DrawBulletTrail(Bullet bullet)
    {
        var direction = bullet.Velocity;
        if (direction.LengthSquared() <= 0f)
        {
            return;
        }

        direction.Normalize();
        FillCircle(bullet.Position - direction * 18f, BulletRadius * 0.7f, new Color(132, 193, 222, 120));
        FillCircle(bullet.Position - direction * 34f, BulletRadius * 0.45f, new Color(78, 132, 170, 70));
    }

    private void DrawTearBullet(Bullet bullet)
    {
        var direction = bullet.Velocity;
        if (direction.LengthSquared() <= 0f)
        {
            FillCircle(bullet.Position, BulletRadius, new Color(184, 226, 244));
            return;
        }

        direction.Normalize();
        var side = new Vector2(-direction.Y, direction.X);

        FillCircle(bullet.Position - direction * 5f, BulletRadius * 1.12f, new Color(82, 139, 181));
        FillCircle(bullet.Position, BulletRadius, new Color(176, 224, 245));
        FillCircle(bullet.Position + direction * 5f, BulletRadius * 0.72f, new Color(210, 240, 250));
        FillCircle(bullet.Position - direction * 2f - side * 3f, BulletRadius * 0.28f, new Color(250, 254, 255));
    }

    private void DrawFloorTiles()
    {
    }

    private void DrawDoors()
    {
        DrawDoor(Up, new RectangleF(Room.Center.X - 72, Room.Top - 48, 144, 52));
        DrawDoor(Down, new RectangleF(Room.Center.X - 72, Room.Bottom - 4, 144, 52));
        DrawDoor(Left, new RectangleF(Room.Left - 48, Room.Center.Y - 72, 52, 144));
        DrawDoor(Right, new RectangleF(Room.Right - 4, Room.Center.Y - 72, 52, 144));
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
            FillCircle(new Vector2(72 + heart * 46, 54), 15, color);
        }

        var clearedCount = floorRooms.Values.Count(room => room.Cleared);
        var status = currentRoom.Cleared ? $"ROOM {clearedCount}/{FloorRoomCount}" : $"ENEMIES: {currentRoom.Enemies.Count}";
        DrawBlockText(status, new Vector2(1470, 38), 4, new Color(241, 228, 208));
        DrawMiniMap();
    }

    private void DrawMiniMap()
    {
        const int cell = 22;
        var origin = new Vector2(74, 96);

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
        var size = enemy.Type == EnemyType.Spider ? EnemyRadius * 2.9f : EnemyRadius * 2.6f;
        var destination = new Rectangle(
            (int)(enemy.Position.X - size / 2f),
            (int)(enemy.Position.Y - size / 2f),
            (int)size,
            (int)size);

        if (enemy.Type == EnemyType.Spider)
        {
            var frame = GetDirectionFrame(enemy);
            spriteBatch.Draw(spiderTexture, destination, new Rectangle(frame * 64, 0, 64, 64), Color.White);
            return;
        }

        var flyFrame = GetDirectionFrame(enemy);
        spriteBatch.Draw(flyTexture, destination, new Rectangle(flyFrame * 64, 0, 64, 64), Color.White);
    }

    private static int GetDirectionFrame(Enemy enemy)
    {
        var direction = enemy.Velocity;
        if (direction.LengthSquared() <= 0.01f)
        {
            return 0;
        }

        if (Math.Abs(direction.X) > Math.Abs(direction.Y))
        {
            return direction.X < 0 ? 2 : 3;
        }

        return direction.Y < 0 ? 1 : 0;
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
        return MathF.Abs(position.X - Room.Center.X) <= 86f;
    }

    private static bool IsInVerticalDoor(Vector2 position)
    {
        return MathF.Abs(position.Y - Room.Center.Y) <= 86f;
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
        ['X'] = ["101", "101", "010", "101", "101"],
        ['Y'] = ["101", "101", "010", "010", "010"],
        ['А'] = ["01110", "10001", "11111", "10001", "10001"],
        ['В'] = ["11110", "10001", "11110", "10001", "11110"],
        ['Д'] = ["01110", "01010", "01010", "11111", "10001"],
        ['Е'] = ["11111", "10000", "11110", "10000", "11111"],
        ['Ж'] = ["10101", "10101", "01110", "10101", "10101"],
        ['З'] = ["11110", "00001", "01110", "00001", "11110"],
        ['И'] = ["10001", "10011", "10101", "11001", "10001"],
        ['Й'] = ["01010", "00100", "10011", "10101", "11001"],
        ['К'] = ["10001", "10010", "11100", "10010", "10001"],
        ['Л'] = ["00111", "01001", "10001", "10001", "10001"],
        ['М'] = ["10001", "11011", "10101", "10001", "10001"],
        ['Н'] = ["10001", "10001", "11111", "10001", "10001"],
        ['О'] = ["01110", "10001", "10001", "10001", "01110"],
        ['П'] = ["11111", "10001", "10001", "10001", "10001"],
        ['Р'] = ["11110", "10001", "11110", "10000", "10000"],
        ['С'] = ["01111", "10000", "10000", "10000", "01111"],
        ['Т'] = ["11111", "00100", "00100", "00100", "00100"],
        ['Х'] = ["10001", "01010", "00100", "01010", "10001"],
        ['Ш'] = ["10101", "10101", "10101", "10101", "11111"],
        ['Ы'] = ["10001", "10001", "11101", "10011", "11101"],
        ['Ь'] = ["10000", "10000", "11110", "10001", "11110"],
        ['Э'] = ["11110", "00001", "01111", "00001", "11110"],
        ['Ю'] = ["10010", "10101", "11101", "10101", "10010"],
    };
}
