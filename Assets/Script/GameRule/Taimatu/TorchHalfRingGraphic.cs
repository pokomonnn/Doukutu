using UnityEngine;
using UnityEngine.UI;

/// <summary>上半分のないドーナツ状の松明ゲージ。素材を追加せずUIメッシュで描画。</summary>
public class TorchHalfRingGraphic : MaskableGraphic
{
    [Range(0,1)] public float remaining=1;
    public void SetRemaining(float value){remaining=Mathf.Clamp01(value);SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Draw(vh,1,new Color(.15f,.17f,.18f,.92f));
        var tint=Color.Lerp(new Color(.9f,.3f,.12f),new Color(1f,.79f,.38f),Mathf.Clamp01(remaining*2));
        Draw(vh,remaining,tint);
    }
    void Draw(VertexHelper vh,float fraction,Color tint)
    {
        if(fraction<=0)return;
        Rect r=rectTransform.rect;float radius=Mathf.Min(r.width*.5f-4,r.height-4);
        Vector2 center=new Vector2(r.center.x,r.yMax-2);
        int segments=Mathf.Max(1,Mathf.CeilToInt(64*fraction));
        for(int i=0;i<segments;i++){
            float a=Mathf.PI+Mathf.PI*fraction*i/segments,b=Mathf.PI+Mathf.PI*fraction*(i+1)/segments;
            Vector2 da=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),db=new Vector2(Mathf.Cos(b),Mathf.Sin(b));int start=vh.currentVertCount;
            vh.AddVert(center+da*radius,tint,Vector2.zero);vh.AddVert(center+db*radius,tint,Vector2.zero);
            vh.AddVert(center+db*(radius-10),tint,Vector2.zero);vh.AddVert(center+da*(radius-10),tint,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
