using UnityEngine;

public class MoveCube : MonoBehaviour
{
    [SerializeField] private float _speed;

    private void Update()
    {
        Vector3 direction = Input.GetAxisRaw("Horizontal") * transform.right;
        direction += Input.GetAxisRaw("Vertical") * transform.forward;

        transform.position += direction.normalized * (_speed * Time.deltaTime);
    }
}
