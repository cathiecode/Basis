using UnityEngine;

[Cilboxable]
public class SetAnimatorBool : MonoBehaviour
{
    [SerializeField] Animator _animator;
    [SerializeField] string _parameterName;
    [SerializeField] bool _isInverted;

    void OnEnable()
    {
        Debug.Log($"SetAnimatorBool {_parameterName} = ${!_isInverted}");
        _animator.SetBool(_parameterName, !_isInverted);
    }

    void OnDisable()
    {
        Debug.Log($"SetAnimatorBool {_parameterName} = {_isInverted}");
        _animator.SetBool(_parameterName, _isInverted);
    }
}
