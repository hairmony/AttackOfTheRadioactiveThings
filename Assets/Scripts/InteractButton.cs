using UnityEngine;

public class InteractButton : MonoBehaviour
{
    private bool activated = false;

    public bool IsActivated()
    {
        return activated;
    }

    public void Interact()
    {
        if (!activated)
        {
            activated = true;
        }
    }
}