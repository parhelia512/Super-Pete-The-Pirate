﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Sprites;
using MonoGame.Extended.Maps.Tiled;
using MonoGame.Extended.ViewportAdapters;
using Microsoft.Xna.Framework.Input;
using Super_Pete_The_Pirate.Characters;
using Super_Pete_The_Pirate.Objects;
using System.Diagnostics;
using Super_Pete_The_Pirate.Sprites;

namespace Super_Pete_The_Pirate.Scenes
{
    class SceneMap : SceneBase
    {
        //--------------------------------------------------
        // Player

        private Player _player;

        //--------------------------------------------------
        // Enemies

        private List<Enemy> _enemies;
        public List<Enemy> Enemies { get { return _enemies; } }

        //--------------------------------------------------
        // Projectiles

        private Dictionary<string, Texture2D> _projectilesTextures;

        private List<GameProjectile> _projectiles;

        //--------------------------------------------------
        // Coins

        private List<AnimatedSprite> _coins;

        //--------------------------------------------------
        // Camera stuff

        private Camera2D _camera;
        private float _cameraSmooth = 0.1f;
        private int _playerCameraOffsetX = 40;

        //--------------------------------------------------
        // Random stuff

        private Random _rand;

        private string mapInfo = "";

        //----------------------//------------------------//

        public Camera2D GetCamera()
        {
            return _camera;
        }

        public override void LoadContent()
        {
            base.LoadContent();

            var viewportSize = SceneManager.Instance.VirtualSize;
            _camera = new Camera2D(SceneManager.Instance.ViewportAdapter);

            // Player init
            _player = new Player(ImageManager.loadCharacter("Player"));

            // Enemies init
            _enemies = new List<Enemy>();

            // Projectiles init
            _projectilesTextures = new Dictionary<string, Texture2D>()
            {
                {"common", ImageManager.loadProjectile("common")}
            };
            _projectiles = new List<GameProjectile>();

            // Coins init
            _coins = new List<AnimatedSprite>();

            _rand = new Random();
            LoadMap(3);
            mapInfo = GameMap.Instance._tiledMap.Layers.ToString();
        }

        private void LoadMap(int mapId)
        {
            GameMap.Instance.LoadMap(Content, mapId);
            SpawnEnemies();
            SpawnPlayer();
            SpawnCoins();
        }

        private void SpawnCoins()
        {
            var coinsGroup = GameMap.Instance.GetObjectGroup("Coins");
            if (coinsGroup == null) return;
            var coinTexture = ImageManager.loadMisc("Coin");
            var coinFrames = new Rectangle[]
            {
                new Rectangle(0, 0, 32, 32),
                new Rectangle(32, 0, 32, 32),
                new Rectangle(64, 0, 32, 32),
                new Rectangle(96, 0, 32, 32)
            };
            var coinBoudingBox = new Rectangle(8, 8, 16, 16);

            foreach (var coinObj in coinsGroup.Objects)
            {
                var coin = new AnimatedSprite(coinTexture, coinFrames, 120, coinObj.X, coinObj.Y - 32);
                coin.SetBoundingBox(coinBoudingBox);
                _coins.Add(coin);
            }
        }

        private void SpawnPlayer()
        {
            var spawnPoint = new Vector2(GameMap.Instance.GetPlayerSpawn().X, GameMap.Instance.GetPlayerSpawn().Y);
            _player.Position = new Vector2(spawnPoint.X, spawnPoint.Y - _player.CharacterSprite.GetColliderHeight());
        }

        private void SpawnEnemies()
        {
            var enemiesGroup = GameMap.Instance.GetObjectGroup("Enemies");
            if (enemiesGroup == null) return;
            var tileSize = GameMap.Instance.TileSize;
            foreach (var enemieObj in enemiesGroup.Objects)
            {
                CreateEnemy(enemieObj, enemieObj.X, enemieObj.Y);
            }
        }

        public void CreateEnemy(TiledObject enemyObj, int x, int y)
        {
            var enemyName = enemyObj.Properties.FirstOrDefault(i => i.Key == "type").Value;
            if (enemyName == null) return;
            var texture = ImageManager.loadCharacter(enemyName);
            var newEnemy = (Enemy)Activator.CreateInstance(Type.GetType("Super_Pete_The_Pirate.Characters." + enemyName), texture);
            newEnemy.Position = new Vector2(x, y - newEnemy.CharacterSprite.GetColliderHeight());
            if (enemyObj.Properties.ContainsKey("FlipHorizontally"))
                newEnemy.CharacterSprite.Effect = enemyObj.Properties["FlipHorizontally"] == "true" ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (enemyName == "TurtleWheel")
                ((TurtleWheel)newEnemy).SetTarget(_player);
            _enemies.Add(newEnemy);
        }

        public void CreateProjectile(string name, Vector2 position, int dx, int dy, int damage, ProjectileSubject subject)
        {
            _projectiles.Add(new GameProjectile(_projectilesTextures[name], position, dx, dy, damage, subject));
        }

        public override void Update(GameTime gameTime)
        {
            _player.Update(gameTime);

            if (InputManager.Instace.KeyPressed(Keys.F)) _projectiles[0].Acceleration = new Vector2(_projectiles[0].Acceleration.X * -1, 0);

            for (var i = 0; i < _projectiles.Count; i++)
            {
                _projectiles[i].Update(gameTime);
                if (_projectiles[i].Subject == ProjectileSubject.FromEnemy && _projectiles[i].BoundingBox.Intersects(_player.BoundingRectangle))
                    _player.ReceiveAttack(_projectiles[i].Damage, _projectiles[i].LastPosition);

                if (_projectiles[i].RequestErase)
                    _projectiles.Remove(_projectiles[i]);
            }

            for (var i = 0; i < _enemies.Count; i++)
            {
                _enemies[i].Update(gameTime);

                if (_enemies[i].HasViewRange && _enemies[i].ViewRangeCooldown <= 0f && _camera.Contains(_enemies[i].BoundingRectangle) != ContainmentType.Disjoint
                    && _enemies[i].ViewRange.Intersects(_player.BoundingRectangle))
                {
                        _enemies[i].PlayerOnSight(_player.Position);
                }

                if (!_enemies[i].Dying && _enemies[i].BoundingRectangle.Intersects(_player.BoundingRectangle))
                {
                    _player.ReceiveAttackWithPoint(1, _enemies[i].BoundingRectangle);
                }

                for (var j = 0; j < _projectiles.Count; j++)
                {
                    if (_projectiles[j].Subject == ProjectileSubject.FromPlayer)
                    {
                        if (!_enemies[i].Dying && !_enemies[i].IsImunity && _projectiles[j].BoundingBox.Intersects(_enemies[i].BoundingRectangle))
                        {
                            if (_enemies[i].EnemyType == EnemyType.TurtleWheel && _enemies[i].InWheelMode)
                            {
                                _projectiles[j].Acceleration = new Vector2(_projectiles[j].Acceleration.X * -1.7f, _rand.Next(-4, 5));
                                CreateSparkParticle(_projectiles[j].Position);
                                _projectiles[j].Subject = ProjectileSubject.FromEnemy;
                            }
                            else
                            {
                                _enemies[i].ReceiveAttack(_projectiles[j].Damage, _projectiles[j].LastPosition);
                                _projectiles[j].Destroy();
                            }
                        }
                    }
                    else if (_projectiles[j].BoundingBox.Intersects(_player.BoundingRectangle))
                    {
                        _player.ReceiveAttack(_projectiles[j].Damage, _projectiles[j].LastPosition);
                        _projectiles[j].Destroy();
                    }

                    if (_projectiles[j].RequestErase)
                        _projectiles.Remove(_projectiles[j]);
                }

                if (_enemies[i].RequestErase)
                    _enemies.Remove(_enemies[i]);
            }

            for (var i = 0; i < _coins.Count; i++)
            {
                _coins[i].Update(gameTime);
                if (_player.BoundingRectangle.Intersects(_coins[i].BoundingBox))
                {
                    _player.AddCoins(1);
                    _coins.Remove(_coins[i]);
                }
            }

            UpdateCamera();
            base.Update(gameTime);

            if (InputManager.Instace.KeyPressed(Keys.P))
            {
                CreateParticle();
            }
        }

        private void CreateParticle()
        {
            var texture = ImageManager.loadParticle("Spark");
            for (var i = 0; i < 4; i++)
            {
                var position = new Vector2(100, 100);
                var velocity = new Vector2(_rand.NextFloat(-100f, 100f), _rand.NextFloat(-100f, 100f));
                var scale = new Vector2(_rand.Next(7, 12), _rand.Next(1, 2));
                var color = ColorUtil.HSVToColor(MathHelper.ToRadians(51f), 0.91f, 0.91f);

                var state = new ParticleState()
                {
                    Velocity = velocity,
                    Type = ParticleType.Spark,
                    UseCustomVelocity = true,
                    VelocityMultiplier = 1f,
                    Width = (int)scale.X,
                    H = 57f
                };

                SceneManager.Instance.ParticleManager.CreateParticle(texture, position, color, 200f, scale, state);
            }
        }

        public void CreateSparkParticle(Vector2 position, int number = 4)
        {
            var texture = ImageManager.loadParticle("Spark");
            for (var i = 0; i < number; i++)
            {
                var velocity = new Vector2(_rand.NextFloat(-100f, 100f), _rand.NextFloat(-100f, 100f));
                var scale = new Vector2(_rand.Next(7, 12), _rand.Next(1, 2));
                var color = ColorUtil.HSVToColor(MathHelper.ToRadians(51f), 0.5f, 0.91f);

                var state = new ParticleState()
                {
                    Velocity = velocity,
                    Type = ParticleType.Spark,
                    UseCustomVelocity = true,
                    VelocityMultiplier = 1f,
                    Width = (int)scale.X,
                    H = 57f
                };

                SceneManager.Instance.ParticleManager.CreateParticle(texture, position, color, 200f, scale, state);
            }
        }

        private void UpdateCamera()
        {
            var size = SceneManager.Instance.WindowSize;
            var viewport = SceneManager.Instance.ViewportAdapter;
            var newPosition = _player.Position - new Vector2(viewport.VirtualWidth / 2f, viewport.VirtualHeight / 2f);
            var playerOffsetX = _playerCameraOffsetX + _player.CharacterSprite.GetColliderWidth() / 2;
            var playerOffsetY = _player.CharacterSprite.GetFrameHeight() / 2;
            var x = MathHelper.Lerp(_camera.Position.X, newPosition.X + playerOffsetX, _cameraSmooth);
            x = MathHelper.Clamp(x, 0.0f, GameMap.Instance.MapWidth - viewport.VirtualWidth);
            var y = MathHelper.Lerp(_camera.Position.Y, newPosition.Y + playerOffsetY, _cameraSmooth);
            y = MathHelper.Clamp(y, 0.0f, GameMap.Instance.MapHeight - viewport.VirtualHeight);
            _camera.Position = new Vector2(x, y);
        }

        public override void Draw(SpriteBatch spriteBatch, ViewportAdapter viewportAdapter)
        {
            base.Draw(spriteBatch, viewportAdapter);
            var debugMode = SceneManager.Instance.DebugMode;

            // Draw the camera (with the map)
            GameMap.Instance.Draw(_camera, spriteBatch);

            spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix(), samplerState: SamplerState.PointClamp);

            // Draw the coins
            for (var i = 0; i < _coins.Count; i++)
            {
                _coins[i].Draw(spriteBatch);
                if (debugMode) _coins[i].DrawCollider(spriteBatch);
            }

            // Draw the player
            _player.DrawCharacter(spriteBatch);
            if (debugMode) _player.DrawColliderBox(spriteBatch);

            // Draw the enemies
            foreach (var enemy in _enemies)
            {
                enemy.DrawCharacter(spriteBatch);
                if (debugMode) enemy.DrawColliderBox(spriteBatch);
            }

            // Draw the projectiles
            foreach (var projectile in _projectiles)
                spriteBatch.Draw(projectile.Sprite);

            // Draw the particles
            SceneManager.Instance.ParticleManager.Draw(spriteBatch);

            spriteBatch.End();
        }
    }
}
