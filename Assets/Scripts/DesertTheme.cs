using UnityEngine;

// Desert Horizon - Canyon Rush: long straights, hairpin, canyon climb, S-curves.
public class DesertTheme : TrackTheme
{
    static readonly Vector3[] points = {
        new Vector3(0,0,-40), new Vector3(0,2,80), new Vector3(20,6,150),
        new Vector3(70,10,170), new Vector3(100,12,130), new Vector3(80,14,80), new Vector3(120,16,40),
        new Vector3(170,18,50), new Vector3(200,15,0), new Vector3(170,10,-50), new Vector3(190,6,-110),
        new Vector3(150,3,-170), new Vector3(90,1,-150), new Vector3(60,0,-200), new Vector3(20,0,-215), new Vector3(0,0,-160) };

    public override string DisplayName => "DESERT HORIZON — CANYON RUSH";
    public override Vector3[] Points => points;
    public override int Seed => 731;
    public override Color Ground => new Color(.82f,.6f,.36f);
    public override Color RockA => new Color(.72f,.36f,.18f);
    public override Color RockB => new Color(.86f,.5f,.25f);
    public override Color Sky => new Color(.95f,.72f,.48f);
    public override float FogEnd => 1100;
    public override Color Ambient => new Color(.72f,.62f,.52f);
    public override Color Mountain => new Color(.71f,.48f,.33f);
    public override Vector3 SunAngles => new Vector3(28, 140, 0);
    public override float SunIntensity => 1.3f;
    public override Color SunColor => new Color(1f,.86f,.68f);

    public override void PlaceScenery(SceneryContext c)
    {
        var rnd = c.rnd;
        float h = 6f + (float)rnd.NextDouble()*(c.trackPoint.y + 22f); // canyon walls rise with elevation
        c.shape("Mesa", PrimitiveType.Cube, c.spot + Vector3.up*h*.45f, new Vector3(6+(float)rnd.NextDouble()*8, h, 6+(float)rnd.NextDouble()*8), rnd.NextDouble() > .5 ? c.rock : c.rock2)
            .transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble()*90f, 0);
        if (rnd.NextDouble() > .8) c.shape("Cactus", PrimitiveType.Cylinder, c.spot + c.side*c.sideSign*5 + Vector3.up*2, new Vector3(.6f, 2, .6f), c.foliage2);
    }
}
