using UnityEngine;

namespace EggTest.Shared
{
    /// <summary>
    /// Shared movement helpers used by client prediction and server authority.
    /// Reusing the exact same logic reduces drift between both sides.
    /// </summary>
    public static class GameMath
    {
        public static Vector2 Cardinalize(Vector2 rawInput)
        {
            if (rawInput.sqrMagnitude < 0.001f)
            {
                return Vector2.zero;
            }

            if (Mathf.Abs(rawInput.x) >= Mathf.Abs(rawInput.y))
            {
                return new Vector2(Mathf.Sign(rawInput.x), 0f);
            }

            return new Vector2(0f, Mathf.Sign(rawInput.y));
        }

        public static Vector3 SimulateKinematicMove(ArenaDefinition arena, Vector3 currentPosition, Vector2 direction, float speed, float deltaTime, float radius)
        {
            if (direction.sqrMagnitude < 0.001f)
            {
                return currentPosition;
            }

            Vector3 step = new Vector3(direction.x, 0f, direction.y) * (speed * deltaTime);
            Vector3 next = currentPosition;

            // Moving one axis at a time avoids tunneling through corners and makes 4-direction movement predictable.
            Vector3 horizontal = next + new Vector3(step.x, 0f, 0f);
            if (CanOccupy(arena, horizontal, radius))
            {
                next = horizontal;
            }

            Vector3 vertical = next + new Vector3(0f, 0f, step.z);
            if (CanOccupy(arena, vertical, radius))
            {
                next = vertical;
            }

            return next;
        }

        public static bool CanOccupy(ArenaDefinition arena, Vector3 worldPosition, float radius)
        {
            float minX = -(arena.Width * arena.CellSize * 0.5f) + radius;
            float maxX = (arena.Width * arena.CellSize * 0.5f) - radius;
            float minZ = -(arena.Height * arena.CellSize * 0.5f) + radius;
            float maxZ = (arena.Height * arena.CellSize * 0.5f) - radius;

            if (worldPosition.x < minX || worldPosition.x > maxX || worldPosition.z < minZ || worldPosition.z > maxZ)
            {
                return false;
            }

            Vector3[] samples =
            {
                worldPosition + new Vector3(radius, 0f, radius),
                worldPosition + new Vector3(radius, 0f, -radius),
                worldPosition + new Vector3(-radius, 0f, radius),
                worldPosition + new Vector3(-radius, 0f, -radius),
            };

            for (int i = 0; i < samples.Length; i++)
            {
                if (!arena.IsWalkable(arena.WorldToCell(samples[i])))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
