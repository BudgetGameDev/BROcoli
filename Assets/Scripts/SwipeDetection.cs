using UnityEngine;
using UnityEngine.InputSystem;

public class SwipeDetection : MonoBehaviour
{
    private InputManager inputManager;
    private PlayerController playerController;

    void Start()
    {
        playerController = gameObject.GetComponent<PlayerController>();
    }

    private void Awake() {
        inputManager = InputManager.Instance;
    }

    private void OnEnable() {
        inputManager.OnUP += UP;
        inputManager.OnDOWN += DOWN;
        inputManager.OnLEFT += LEFT;
        inputManager.OnRIGHT += RIGHT;
    }

    private void OnDisable() {
        inputManager.OnUP -= UP;
        inputManager.OnDOWN -= DOWN;
        inputManager.OnLEFT -= LEFT;
        inputManager.OnRIGHT -= RIGHT;
    }

    private void UP(float axis) {
        if (axis == 1) {
            Debug.Log("Keyboard UP");
            //playerController.QueueMove(Direction.UP); 
        }
    }

    private void DOWN(float axis) {
        if (axis == 1) {
            Debug.Log("Keyboard DOWN");
            //playerController.QueueMove(Direction.DOWN);
        }
    }

    private void LEFT(float axis) {
        if (axis == 1) {
            Debug.Log("Keyboard LEFT");
            //playerController.QueueMove(Direction.LEFT);
        }
    }

    private void RIGHT(float axis) {
        if (axis == 1) {
            Debug.Log("Keyboard RIGHT");
            //playerController.QueueMove(Direction.RIGHT);
        }
    }
}
