using UnityEngine;

// Rally Pioneers - shared base for the desert circuit and its roadside scenery.
public delegate GameObject ShapeMaker(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat);

public struct SceneryContext
{
    public Vector3 spot;      // ground position for the prop
    public Vector3 trackPoint;// nearest track point (uses its height)
    public Vector3 side;      // sideways direction from the track
    public int sideSign;      // -1 left, +1 right
    public System.Random rnd;
    public ShapeMaker shape;
    public Material foliage2, rock, rock2;
}

public abstract class TrackTheme
{
    public abstract string DisplayName { get; }
    public abstract Vector3[] Points { get; }
    public abstract int Seed { get; }
    public abstract Color Ground { get; }
    public abstract Color RockA { get; }
    public abstract Color RockB { get; }
    public abstract Color Sky { get; }
    public abstract float FogEnd { get; }
    public abstract Color Ambient { get; }
    public abstract Color Mountain { get; }
    public abstract Vector3 SunAngles { get; }
    public abstract float SunIntensity { get; }
    public abstract Color SunColor { get; }
    public abstract void PlaceScenery(SceneryContext c);
}
