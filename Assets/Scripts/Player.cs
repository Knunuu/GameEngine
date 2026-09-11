using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;

    private InputSystem_Actions _inputActions;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        _inputActions = new InputSystem_Actions();
        _inputActions.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());
    }

    void OnDestroy()
    {
        _inputActions.Dispose();
    }

    void Update()
    {
        var input = _inputActions.Player;

        // Update camera rotation based on input
        var cameraInput = new CameraInput
        {
            LookAxis = input.Look.ReadValue<Vector2>()
        };
        playerCamera.UpdateRotation(cameraInput);

        // Update character movement based on input
        var characterInput = new CharacterInput
        {
            Rotation = playerCamera.transform.rotation,
            Move = input.Move.ReadValue<Vector2>(),
            Jump = input.Jump.triggered,
            JumpSustain = input.Jump.IsPressed(),
            Crouch = input.Crouch.IsPressed() ? CrouchInput.Hold : CrouchInput.None
        };
        playerCharacter.UpdateInput(characterInput);
        playerCharacter.UpdateBody(Time.deltaTime);

    }

    void LateUpdate()
    {
        playerCamera.UpdatePosition(playerCharacter.GetCameraTarget());
    }
}
