using EggTest.Shared;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EggTest.Client
{
    /// <summary>
    /// Builds and repairs the scene-side world contract and arena visuals.
    /// Keeps environment generation out of the runtime flow controller.
    /// </summary>
    public sealed class ArenaSceneBuilder
    {
        private enum MaterialRole
        {
            Floor,
            Obstacle,
            Border,
        }

        private const string WorldRootName = "World";
        private const string ArenaRootName = "Arena";
        private const string ObstaclesRootName = "Obstacles";
        private const string PlayersRootName = "Players";
        private const string EggsRootName = "Eggs";
        private const string HudObjectName = "HUD";
        public static readonly Color FloorColor = new Color(0.16f, 0.20f, 0.24f);
        public static readonly Color ObstacleColor = new Color(0.90f, 0.94f, 0.98f);
        public static readonly Color BorderColor = new Color(0.09f, 0.12f, 0.17f);

        private Material _floorMaterial;
        private Material _obstacleMaterial;
        private Material _borderMaterial;

        public SceneContract ResolveSceneContract(Transform gameRoot, HudPresenter existingHud, bool createMissing)
        {
            Transform worldRoot = ResolveChild(gameRoot, WorldRootName, createMissing);
            Transform arenaRoot = ResolveChild(worldRoot, ArenaRootName, createMissing);
            Transform obstaclesRoot = ResolveChild(arenaRoot, ObstaclesRootName, createMissing);
            Transform playersRoot = ResolveChild(worldRoot, PlayersRootName, createMissing);
            Transform eggsRoot = ResolveChild(worldRoot, EggsRootName, createMissing);

            HudPresenter hud = existingHud;
            if (hud == null)
            {
                Transform hudTransform = gameRoot.Find(HudObjectName);
                if (hudTransform != null)
                {
                    hud = hudTransform.GetComponent<HudPresenter>();
                }
            }

            if (hud == null && createMissing)
            {
                GameObject hudObject = new GameObject(HudObjectName);
                hudObject.transform.SetParent(gameRoot, false);
                hud = hudObject.AddComponent<HudPresenter>();
            }

            return new SceneContract(worldRoot, arenaRoot, obstaclesRoot, playersRoot, eggsRoot, hud);
        }

        public void EnsureRuntimeArenaVisuals(SceneContract scene, ArenaDefinition arena)
        {
            if (scene == null || scene.ArenaRoot == null || scene.ObstaclesRoot == null)
            {
                return;
            }

            if (!HasAuthoredArenaVisuals(scene))
            {
                EnsureArenaVisuals(scene, arena, rebuildObstacles: true, immediate: false);
            }
            else
            {
                EnsureArenaVisuals(scene, arena, rebuildObstacles: false, immediate: false);
            }
        }

        public void EnsureEditorArenaVisuals(SceneContract scene, ArenaDefinition arena)
        {
            if (scene == null || scene.ArenaRoot == null || scene.ObstaclesRoot == null)
            {
                return;
            }

            EnsureArenaVisuals(scene, arena, rebuildObstacles: true, immediate: true);
        }

        public void ClearRuntimeDynamicObjects(SceneContract scene)
        {
            if (scene == null)
            {
                return;
            }

            ClearChildren(scene.PlayersRoot, immediate: false);
            ClearChildren(scene.EggsRoot, immediate: false);
        }

        public void DestroySceneChildrenImmediate(Transform gameRoot)
        {
            if (gameRoot == null)
            {
                return;
            }

            for (int index = gameRoot.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(gameRoot.GetChild(index).gameObject);
            }
        }

        public void DestroyEventSystemsImmediate()
        {
            EventSystem[] eventSystems = Object.FindObjectsOfType<EventSystem>(true);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                if (eventSystems[i] != null)
                {
                    Object.DestroyImmediate(eventSystems[i].gameObject);
                }
            }
        }

        private bool HasAuthoredArenaVisuals(SceneContract scene)
        {
            return scene.ArenaRoot != null
                && scene.ObstaclesRoot != null
                && scene.ArenaRoot.Find("Floor") != null
                && scene.ObstaclesRoot.childCount > 0;
        }

        private void EnsureArenaVisuals(SceneContract scene, ArenaDefinition arena, bool rebuildObstacles, bool immediate)
        {
            if (scene == null || scene.ArenaRoot == null || scene.ObstaclesRoot == null || arena == null)
            {
                return;
            }

            float arenaWidth = arena.Width * arena.CellSize;
            float arenaHeight = arena.Height * arena.CellSize;

            GameObject floor = GetOrCreatePrimitive(scene.ArenaRoot, "Floor", PrimitiveType.Cube);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(arenaWidth, 0.5f, arenaHeight);
            ApplyRendererStyle(floor, MaterialRole.Floor);

            if (rebuildObstacles)
            {
                ClearChildren(scene.ObstaclesRoot, immediate);
                foreach (GridCell blockedCell in arena.BlockedCells)
                {
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Wall_" + blockedCell.X + "_" + blockedCell.Y;
                    wall.transform.SetParent(scene.ObstaclesRoot, false);
                    wall.transform.position = arena.CellToWorld(blockedCell) + new Vector3(0f, 0.5f, 0f);
                    wall.transform.localScale = new Vector3(arena.CellSize, 1.0f, arena.CellSize);
                    ApplyRendererStyle(wall, MaterialRole.Obstacle);
                }
            }
            else
            {
                NormalizeExistingObstacleColors(scene);
            }

            EnsureArenaBorder(scene, arena);
        }

        private void EnsureArenaBorder(SceneContract scene, ArenaDefinition arena)
        {
            if (scene == null || scene.ArenaRoot == null || arena == null)
            {
                return;
            }

            float arenaWidth = arena.Width * arena.CellSize;
            float arenaHeight = arena.Height * arena.CellSize;

            EnsureBorderWall(scene.ArenaRoot, "NorthBorder", new Vector3(0f, 0.55f, arenaHeight * 0.5f + 0.25f), new Vector3(arenaWidth + 1f, 1.1f, 0.5f));
            EnsureBorderWall(scene.ArenaRoot, "SouthBorder", new Vector3(0f, 0.55f, -arenaHeight * 0.5f - 0.25f), new Vector3(arenaWidth + 1f, 1.1f, 0.5f));
            EnsureBorderWall(scene.ArenaRoot, "WestBorder", new Vector3(-arenaWidth * 0.5f - 0.25f, 0.55f, 0f), new Vector3(0.5f, 1.1f, arenaHeight + 1f));
            EnsureBorderWall(scene.ArenaRoot, "EastBorder", new Vector3(arenaWidth * 0.5f + 0.25f, 0.55f, 0f), new Vector3(0.5f, 1.1f, arenaHeight + 1f));
        }

        private void EnsureBorderWall(Transform arenaRoot, string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GetOrCreatePrimitive(arenaRoot, name, PrimitiveType.Cube);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;
            ApplyRendererStyle(wall, MaterialRole.Border);
        }

        private void NormalizeExistingObstacleColors(SceneContract scene)
        {
            if (scene == null || scene.ObstaclesRoot == null)
            {
                return;
            }

            for (int i = 0; i < scene.ObstaclesRoot.childCount; i++)
            {
                Transform child = scene.ObstaclesRoot.GetChild(i);
                if (child != null)
                {
                    ApplyRendererStyle(child.gameObject, MaterialRole.Obstacle);
                }
            }
        }

        private Transform ResolveChild(Transform parent, string name, bool createMissing)
        {
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find(name);
            if (existing != null || !createMissing)
            {
                return existing;
            }

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private GameObject GetOrCreatePrimitive(Transform parent, string name, PrimitiveType primitiveType)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject created = GameObject.CreatePrimitive(primitiveType);
            created.name = name;
            created.transform.SetParent(parent, false);
            return created;
        }

        private void ApplyRendererStyle(GameObject target, MaterialRole role)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = GetOrCreateMaterial(role);
        }

        private Material GetOrCreateMaterial(MaterialRole role)
        {
            switch (role)
            {
                case MaterialRole.Floor:
                    if (_floorMaterial == null)
                    {
                        _floorMaterial = CreateMaterial("Arena_Floor_Runtime", FloorColor);
                    }
                    return _floorMaterial;

                case MaterialRole.Border:
                    if (_borderMaterial == null)
                    {
                        _borderMaterial = CreateMaterial("Arena_Border_Runtime", BorderColor);
                    }
                    return _borderMaterial;

                default:
                    if (_obstacleMaterial == null)
                    {
                        _obstacleMaterial = CreateMaterial("Arena_Obstacle_Runtime", ObstacleColor);
                    }
                    return _obstacleMaterial;
            }
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = materialName;
            material.color = color;
            return material;
        }

        private void ClearChildren(Transform root, bool immediate)
        {
            if (root == null)
            {
                return;
            }

            for (int index = root.childCount - 1; index >= 0; index--)
            {
                GameObject child = root.GetChild(index).gameObject;
                if (immediate)
                {
                    Object.DestroyImmediate(child);
                }
                else
                {
                    Object.Destroy(child);
                }
            }
        }
    }
}
