using UnityEngine;

// Forest Run - Woodland Fury: tight technical sections, chicanes, hill crest and dip.
public class ForestTheme : TrackTheme
{
    static readonly Vector3[] points = {
        new Vector3(-10,1,-30), new Vector3(15,4,30), new Vector3(-20,7,80),
        new Vector3(-80,9,95), new Vector3(-130,6,60), new Vector3(-110,3,10), new Vector3(-150,2,-30),
        new Vector3(-120,4,-90), new Vector3(-60,8,-80), new Vector3(-40,5,-140), new Vector3(-80,2,-190),
        new Vector3(-30,0,-230), new Vector3(20,0,-200), new Vector3(0,0,-120) };

    public override string DisplayName => "FOREST RUN — WOODLAND FURY";
    public override Vector3[] Points => points;
    public override int Seed => 1907;
    public override Color Ground => new Color(.2f,.36f,.18f);
    public override Color RockA => new Color(.4f,.42f,.4f);
    public override Color RockB => new Color(.3f,.32f,.3f);
    public override Color Sky => new Color(.55f,.72f,.8f);
    public override float FogEnd => 900;
    public override Color Ambient => new Color(.5f,.58f,.55f);
    public override Color Mountain => new Color(.36f,.5f,.53f);
    public override Vector3 SunAngles => new Vector3(38, -35, 0);
    public override float SunIntensity => 1.05f;
    public override Color SunColor => new Color(1f,.96f,.88f);

    public override void PlaceScenery(SceneryContext c)
    {
        var rnd = c.rnd;
        float h = 7f + (float)rnd.NextDouble()*9f;
        c.shape("Trunk", PrimitiveType.Cylinder, c.spot + Vector3.up*h*.3f, new Vector3(.7f, h*.3f, .7f), c.bark);
        c.shape("Pine", PrimitiveType.Cube, c.spot + Vector3.up*h*.65f, new Vector3(4.5f, h*.55f, 4.5f), rnd.NextDouble() > .5 ? c.foliage : c.foliage2).transform.rotation = Quaternion.Euler(0, 45, 0);
        c.shape("Crown", PrimitiveType.Sphere, c.spot + Vector3.up*h*.95f, new Vector3(3, 3, 3), c.foliage2);
    }
}
