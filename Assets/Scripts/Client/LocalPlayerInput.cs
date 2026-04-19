using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EggTest.Client
{
    /// <summary>
    /// Thin wrapper around the project's canonical Input Actions asset.
    /// Keeps gameplay code independent from the specific input backend.
    /// </summary>
    public sealed class LocalPlayerInput : IDisposable
    {
        private const string ResourcePath = "Input/EggTestControls";

        private readonly InputActionAsset _actionsAsset;
        private readonly InputActionMap _gameplayMap;
        private readonly InputAction _moveAction;

        public InputActionAsset ActionsAsset
        {
            get { return _actionsAsset; }
        }

        public LocalPlayerInput(InputActionAsset configuredAsset)
        {
            _actionsAsset = InstantiateRequiredAsset(configuredAsset);
            _gameplayMap = _actionsAsset.FindActionMap("Gameplay", true);
            _moveAction = _gameplayMap.FindAction("Move", true);
            _actionsAsset.Enable();
        }

        public static InputActionAsset LoadConfiguredAsset()
        {
            return Resources.Load<InputActionAsset>(ResourcePath);
        }

        public Vector2 ReadMove()
        {
            return _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        public void Dispose()
        {
            if (_gameplayMap != null)
            {
                _gameplayMap.Disable();
            }

            if (_actionsAsset != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_actionsAsset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_actionsAsset);
                }
            }
        }

        private static InputActionAsset InstantiateRequiredAsset(InputActionAsset configuredAsset)
        {
            InputActionAsset instantiatedAsset = configuredAsset != null ? UnityEngine.Object.Instantiate(configuredAsset) : null;
            if (instantiatedAsset == null)
            {
                throw new InvalidOperationException("[EggTest] Missing Input Actions asset at Resources/Input/EggTestControls.");
            }

            return instantiatedAsset;
        }

    }
}
