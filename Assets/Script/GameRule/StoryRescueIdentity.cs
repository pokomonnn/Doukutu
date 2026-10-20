using UnityEngine;

// The extraction collector is the only writer of this persistent alive-rescue flag.
public sealed class StoryRescueIdentity : MonoBehaviour
{
    public string rescuedFlag = "S1S01_Rescued";
    public bool AlreadyRescued => StoryCampaignRuntime.Has(rescuedFlag);
    void Start() { if (GetComponent<StoryFinnDelivery>() == null && AlreadyRescued) gameObject.SetActive(false); }
}
