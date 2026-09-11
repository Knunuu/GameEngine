using UnityEngine;
using KinematicCharacterController;

public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public CrouchInput Crouch;
} 

public enum CrouchInput
{
    None,
    Hold
}

public enum Stance
{
    Stand,
    Crouch,
    Slide
}

public struct CharacterState
{
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [Header("References")]
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform cameraTarget;

    [Header("Ground Movement")]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;
    [SerializeField] private float walkTransitionSpeed = 25f;
    [SerializeField] private float crouchTransitionSpeed = 20f;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightTransitionSpeed = 15f;
    [Space]
    [SerializeField] private float slideStartSpeed = 25f; //how fast slide starts when activated
    [SerializeField] private float slideEndSpeed = 15f; //deactivates slide when speed drops below this value
    [SerializeField] private float slideFriction = 0.8f;
    [SerializeField] private float slideSteerAcceleration = 5f; //how fast you can steer while sliding
    [SerializeField] private float slideGravity = -80f;
    
    [Range(0f, 1f)]
    [SerializeField] private float cameraStandHeight = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float cameraCrouchHeight = 0.2f;

    [Header("Air Movement")]
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float airAcceleration = 70f;
    [Space]
    [SerializeField] private float jumpSpeed = 20f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpSustainGravity = 0.4f; //multiplier
    [SerializeField] private float gravity = -80f;

    private CharacterState _state, _lastState, _tempState;

    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedSustainJump;
    private bool _requestedCrouch;
    private bool _requestedCrouchInAir;

    private Collider[] _uncrouchingOverlaps;

    public void Initialize()
    {
        motor.CharacterController = this;
        _state.Stance = Stance.Stand;
        _lastState = _state;
        _uncrouchingOverlaps = new Collider[8];
    }

    public void UpdateInput(CharacterInput input)
    {
        _requestedRotation = input.Rotation;

        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
        _requestedMovement = input.Rotation * _requestedMovement;
        _requestedMovement = _requestedMovement.normalized;

        _requestedJump = _requestedJump || input.Jump;
        _requestedSustainJump = input.JumpSustain;

        var wasRequestingCrouch  = _requestedCrouch;
        _requestedCrouch = input.Crouch == CrouchInput.Hold;
        if (_requestedCrouch && !wasRequestingCrouch)
        {
            _requestedCrouchInAir = !_state.Grounded;
        }
        else if (!_requestedCrouch && wasRequestingCrouch)
        {
            _requestedCrouchInAir = false;
        }

        Debug.Log($"Requested Movement: {_requestedMovement}, Jump: {_requestedJump}, Crouch: {_requestedCrouch}, Stance: {_state.Stance}");
    }

    public void UpdateBody(float deltaTime)
    {
        var currentHeight = motor.Capsule.height;
        var cameraTargetHeight = _state.Stance == Stance.Stand ? cameraStandHeight : cameraCrouchHeight; 
        cameraTarget.localPosition = Vector3.Lerp
        (
            a: cameraTarget.localPosition,
            b: new Vector3 (0f, cameraTargetHeight, 0f),
            t: 1f - Mathf.Exp(-crouchHeightTransitionSpeed * deltaTime)
        );
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        var forward = Vector3.ProjectOnPlane
        (
            _requestedRotation * Vector3.forward, 
            motor.CharacterUp
        ).normalized;
        currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {

        if (motor.GroundingStatus.IsStableOnGround)
        {
            var groundedMovement = motor.GetDirectionTangentToSurface
            (
                direction: _requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _requestedMovement.magnitude;

            //Sliding Movement
            var moving = groundedMovement.sqrMagnitude > 0f;
            var crouching = _state.Stance == Stance.Crouch;
            var wasStanding = _lastState.Stance == Stance.Stand;
            var wasInAir = !_lastState.Grounded;

            if (moving && crouching && (wasStanding || wasInAir))
            {
                _state.Stance = Stance.Slide;

                if (wasInAir)
                {
                    currentVelocity = Vector3.ProjectOnPlane
                    (
                        vector: _lastState.Velocity,
                        planeNormal: motor.GroundingStatus.GroundNormal
                    );
                }

                var effectiveSlideStartSpeed = slideStartSpeed;
                if (!_lastState.Grounded && !_requestedCrouchInAir)
                {
                    effectiveSlideStartSpeed = 0f;
                    _requestedCrouchInAir = false;
                }
                var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                currentVelocity = motor.GetDirectionTangentToSurface
                (
                    direction: currentVelocity,
                    surfaceNormal: motor.GroundingStatus.GroundNormal
                ).normalized * slideSpeed;

            }

            
            //grounded 
            if (_state.Stance == Stance.Stand || _state.Stance == Stance.Crouch)
            {
                var moveSpeed = _state.Stance == Stance.Stand ? walkSpeed : crouchSpeed;

                var transitionSpeed = _state.Stance == Stance.Stand ? walkTransitionSpeed : crouchTransitionSpeed;

                var targetVelocity = groundedMovement * moveSpeed;
                currentVelocity = Vector3.Lerp
                (
                    a: currentVelocity,
                    b: targetVelocity,
                    t: 1f - Mathf.Exp(-transitionSpeed * deltaTime)
                );
            }
            else //sliding continue
            {
                currentVelocity -= currentVelocity * slideFriction * deltaTime;

                //sliding on slopes
                var force = Vector3.ProjectOnPlane
                (
                    vector: -motor.CharacterUp,
                    planeNormal: motor.GroundingStatus.GroundNormal
                ) * slideGravity;

                currentVelocity -= force * deltaTime;

                //steering while sliding
                var currentSpeed = currentVelocity.magnitude;
                var targetVelocity = groundedMovement * currentVelocity.magnitude;
                var steerForce = (targetVelocity - currentVelocity) * slideSteerAcceleration * deltaTime;
                currentVelocity += steerForce;
                currentVelocity = Vector3.ClampMagnitude(currentVelocity, currentSpeed); //cant steer faster than current speed

                if (currentVelocity.magnitude < slideEndSpeed)
                {
                    _state.Stance = Stance.Crouch;
                }
            }
        }
        else //airborne
        {
            if(_requestedMovement.sqrMagnitude > 0f)
            {
                var planarMovement = Vector3.ProjectOnPlane
                (
                    vector: _requestedMovement,
                    planeNormal: motor.CharacterUp
                ).normalized * _requestedMovement.magnitude;

                var currentPlanarVelocity = Vector3.ProjectOnPlane
                (
                    vector: currentVelocity,
                    planeNormal: motor.CharacterUp
                );

                var movementForce = planarMovement * airAcceleration * deltaTime;

                if (currentPlanarVelocity.magnitude < airSpeed)
                {
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                    movementForce = targetPlanarVelocity - currentPlanarVelocity;
                }
                else if (Vector3.Dot(currentPlanarVelocity, movementForce) > 0f)
                {
                    var constrainedMovementForce = Vector3.ProjectOnPlane
                    (
                        vector: movementForce,
                        planeNormal: currentPlanarVelocity.normalized
                    ); 

                    movementForce = constrainedMovementForce;
                }

                if (motor.GroundingStatus.FoundAnyGround) //if moving into wall that is too steep to stand on, we want to prevent the player from being able to "climb" up the wall by holding forward
                {
                    if (Vector3.Dot(movementForce, currentVelocity + movementForce) > 0f)
                    {
                        var obstructionNormal = Vector3.Cross
                        (
                            motor.CharacterUp,
                            Vector3.Cross
                            (
                                motor.CharacterUp,
                                motor.GroundingStatus.GroundNormal
                            )
                        ).normalized;

                        movementForce = Vector3.ProjectOnPlane
                        (
                            vector: movementForce,
                            planeNormal: obstructionNormal
                        );
                    }
                }

                currentVelocity += movementForce;
            }

            var effectiveGravity = gravity;
            if (_requestedSustainJump && Vector3.Dot(currentVelocity, motor.CharacterUp) > 0f)
            {
                effectiveGravity *= jumpSustainGravity;
            }

            currentVelocity += effectiveGravity * deltaTime * motor.CharacterUp;
        }

        if (_requestedJump)
        {
            var grounded = motor.GroundingStatus.IsStableOnGround;

            if (grounded)
            {
                _requestedJump = false;
                _requestedCrouch = false;
                _requestedCrouchInAir = false;
                motor.ForceUnground(time: 0f);
                
                var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }
            else
            {
                _requestedJump = false;
            }
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        _tempState = _state;

        if (_state.Stance == Stance.Slide && !_requestedCrouch)
        {
            _state.Stance = Stance.Stand;
        }

        //crouching 
        if (_requestedCrouch && _state.Stance == Stance.Stand)
        {
            _state.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: crouchHeight, 
                yOffset: crouchHeight * 0.5f - 1f
            );
        }
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (!motor.GroundingStatus.IsStableOnGround && _state.Stance == Stance.Slide)
        {
            _state.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: crouchHeight, 
                yOffset: crouchHeight * 0.5f - 1f
            );
        }
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (!_requestedCrouch && _state.Stance == Stance.Slide)
        {
            _state.Stance = Stance.Stand;
        }

        if (!_requestedCrouch && _state.Stance == Stance.Crouch)
        {
            // Check if we can stand up by uncrhouching briefly and checking for overlaps
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: standHeight, 
                yOffset: standHeight * 0.5f - 1f
            );

            var pos = motor.TransientPosition;
            var rot = motor.TransientRotation;
            var mask = motor.CollidableLayers;
            if (motor.CharacterOverlap(pos, rot, _uncrouchingOverlaps, mask, QueryTriggerInteraction.Ignore) > 0)
            {
                // If we have any overlaps, we can't stand up, so go back to crouching
                _requestedCrouch = true;
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: crouchHeight, 
                    yOffset: crouchHeight * 0.5f - 1f
                );
            }
            else // If we have no overlaps, we can stand up
            {
                _state.Stance = Stance.Stand;
            }
        }

        _state.Grounded = motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = motor.Velocity;
        _lastState = _tempState;
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void ProcessHitStabilityReport(
        Collider hitCollider,
        Vector3 hitNormal,
        Vector3 hitPoint,
        Vector3 atCharacterPosition,
        Quaternion incomingRotation,
        ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    public Transform GetCameraTarget() => cameraTarget;
}