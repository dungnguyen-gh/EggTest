using System;
using System.Collections.Generic;
using EggTest.Shared;
using UnityEngine;

namespace EggTest.Client
{
    public sealed class PlayerView : MonoBehaviour
    {
        private struct BufferedSnapshot
        {
            public double ServerTime;
            public Vector3 Position;
            public Vector2 Direction;
        }

        private readonly List<BufferedSnapshot> _snapshotBuffer = new List<BufferedSnapshot>();

        private Transform _visualRoot;
        private Transform _labelTransform;
        private Renderer _renderer;
        private TextMesh _label;
        private PlayerProfile _profile;
        private int _score;
        private Camera _cachedMainCamera;

        public void Initialize(PlayerProfile profile)
        {
            _profile = profile;
            GameTrace.Verbose("View", "Initializing player view for " + profile.DisplayName + ".");

            _visualRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            _visualRoot.name = "Body";
            _visualRoot.SetParent(transform, false);
            _visualRoot.localPosition = new Vector3(0f, 0.5f, 0f);
            _visualRoot.localScale = new Vector3(0.7f, 0.5f, 0.7f);

            _renderer = _visualRoot.GetComponent<Renderer>();
            _renderer.material = new Material(Shader.Find("Standard"));
            _renderer.material.color = profile.Color;

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            _labelTransform = labelObject.transform;
            _label = labelObject.AddComponent<TextMesh>();
            _label.fontSize = 32;
            _label.characterSize = 0.1f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.text = profile.DisplayName;

            SetScore(0);
        }

        private void LateUpdate()
        {
            UpdateLabelFacingCamera();
        }

        public void SetScore(int score)
        {
            _score = score;
            if (_label != null)
            {
                _label.text = _profile.DisplayName + "\n" + _score;
            }
        }

        public void SetLocalPredictedState(Vector3 position, Vector2 direction)
        {
            transform.position = position;
            UpdateFacing(direction);
        }

        public void PushRemoteSnapshot(PlayerSnapshot snapshot, double serverTime)
        {
            BufferedSnapshot incoming = new BufferedSnapshot
            {
                ServerTime = serverTime,
                Position = snapshot.Position,
                Direction = snapshot.MoveDirection,
            };

            if (_snapshotBuffer.Count == 0 || _snapshotBuffer[_snapshotBuffer.Count - 1].ServerTime <= incoming.ServerTime)
            {
                _snapshotBuffer.Add(incoming);
            }
            else
            {
                int insertIndex = _snapshotBuffer.Count;
                while (insertIndex > 0 && _snapshotBuffer[insertIndex - 1].ServerTime > incoming.ServerTime)
                {
                    insertIndex--;
                }

                _snapshotBuffer.Insert(insertIndex, incoming);
            }

            while (_snapshotBuffer.Count > 12)
            {
                _snapshotBuffer.RemoveAt(0);
            }
        }

        public void TickRemote(double estimatedServerTime, float interpolationBackTime, float playerMoveSpeed, float extrapolationLimit)
        {
            if (_snapshotBuffer.Count == 0)
            {
                return;
            }

            double renderTime = estimatedServerTime - interpolationBackTime;
            while (_snapshotBuffer.Count >= 2 && _snapshotBuffer[1].ServerTime <= renderTime)
            {
                _snapshotBuffer.RemoveAt(0);
            }

            BufferedSnapshot first = _snapshotBuffer[0];
            BufferedSnapshot second = _snapshotBuffer.Count > 1 ? _snapshotBuffer[1] : first;
            Vector3 position;
            Vector2 direction;

            if (_snapshotBuffer.Count > 1 && renderTime >= first.ServerTime && renderTime <= second.ServerTime)
            {
                double span = Math.Max(0.0001, second.ServerTime - first.ServerTime);
                float t = (float)((renderTime - first.ServerTime) / span);
                position = Vector3.Lerp(first.Position, second.Position, t);
                direction = Vector2.Lerp(first.Direction, second.Direction, t);
            }
            else if (renderTime > first.ServerTime)
            {
                float extrapolationTime = Mathf.Min((float)(renderTime - first.ServerTime), extrapolationLimit);
                position = first.Position + new Vector3(first.Direction.x, 0f, first.Direction.y) * (playerMoveSpeed * extrapolationTime);
                direction = first.Direction;
            }
            else
            {
                position = first.Position;
                direction = first.Direction;
            }

            transform.position = position;
            UpdateFacing(direction);
            GameTrace.LogEvery("View", "RemotePlayer_" + _profile.Id.Value, 1.0f, _profile.DisplayName + " rendered at " + position + " using " + _snapshotBuffer.Count + " buffered snapshots.", verboseOnly: true);
        }

        private void UpdateFacing(Vector2 direction)
        {
            if (_visualRoot == null || direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            _visualRoot.forward = new Vector3(direction.x, 0f, direction.y);
        }

        private void UpdateLabelFacingCamera()
        {
            if (_labelTransform == null)
            {
                return;
            }

            if (_cachedMainCamera == null || !_cachedMainCamera.isActiveAndEnabled)
            {
                _cachedMainCamera = Camera.main;
            }

            if (_cachedMainCamera == null)
            {
                return;
            }

            Vector3 toLabel = _labelTransform.position - _cachedMainCamera.transform.position;
            if (toLabel.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _labelTransform.rotation = Quaternion.LookRotation(toLabel.normalized, _cachedMainCamera.transform.up);
        }
    }

    public sealed class EggView : MonoBehaviour
    {
        private Transform _visual;
        private Renderer _renderer;
        private Vector3 _basePosition;

        public void Initialize()
        {
            GameTrace.Verbose("View", "Initializing egg view.");
            _visual = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _visual.name = "Visual";
            _visual.SetParent(transform, false);
            _visual.localScale = new Vector3(0.5f, 0.65f, 0.5f);

            _renderer = _visual.GetComponent<Renderer>();
            _renderer.material = new Material(Shader.Find("Standard"));
        }

        public void ApplySnapshot(EggSnapshot snapshot, Color[] palette)
        {
            _basePosition = snapshot.Position;
            transform.position = snapshot.Position;
            if (_renderer != null && palette != null && palette.Length > 0)
            {
                _renderer.material.color = palette[snapshot.PaletteIndex % palette.Length];
            }
        }

        public void Tick(double now)
        {
            if (_visual == null)
            {
                return;
            }

            float bob = Mathf.Sin((float)now * 4f) * 0.08f;
            transform.position = _basePosition + new Vector3(0f, bob, 0f);
            _visual.Rotate(Vector3.up, 60f * Time.unscaledDeltaTime, Space.Self);
        }
    }

}
