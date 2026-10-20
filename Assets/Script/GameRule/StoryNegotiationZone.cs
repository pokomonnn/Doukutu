using System.Collections.Generic;
using UnityEngine;

// Local ceasefire; no mutation of the global faction-rules asset.
public sealed class StoryNegotiationZone : MonoBehaviour
{
    public Vector2 size = new Vector2(12,8);
    public string requiredFlag;
    static readonly HashSet<StoryNegotiationZone> zones = new HashSet<StoryNegotiationZone>();
    void OnEnable() => zones.Add(this);
    void OnDisable() => zones.Remove(this);
    bool Contains(Vector3 position) => new Bounds(transform.position,new Vector3(size.x,size.y,100)).Contains(position);
    public static bool Protects(FactionMember2D a, FactionMember2D b)
    {
        if(a==null || b==null) return false;
        // Negotiations settle human/faction disputes, not attacks by dungeon monsters.
        if(a.FactionId==FactionId2D.Monsters || b.FactionId==FactionId2D.Monsters) return false;
        foreach(var zone in zones)
            if(zone!=null && (string.IsNullOrEmpty(zone.requiredFlag)||StoryCampaignRuntime.Has(zone.requiredFlag)) && zone.Contains(a.transform.position) && zone.Contains(b.transform.position)) {
                if(zone.requiredFlag=="S3S01_Completed") {
                    bool pair=(a.FactionId==FactionId2D.Adventurers && b.FactionId==FactionId2D.Bandits) ||
                        (b.FactionId==FactionId2D.Adventurers && a.FactionId==FactionId2D.Bandits);
                    if(!pair) continue;
                }
                return true;
            }
        return false;
    }
    void OnDrawGizmosSelected() {Gizmos.color=Color.cyan;Gizmos.DrawWireCube(transform.position,size);}
}
