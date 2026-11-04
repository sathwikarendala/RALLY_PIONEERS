using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rally Pioneers - Desert Championship.
// Fully procedural (no external assets). A spline circuit with elevation,
// mesh road + collider, 5 AI rivals, live race positions, results table.
public class RallyGame : MonoBehaviour
{
    const float Width = 20f;
    const int Laps = 3;
    const int Rivals = 5;
    static readonly string[] RivalNames = { "M. Rossi", "K. Tanaka", "L. Weber", "A. Silva", "J. Okafor" };
    static readonly Color[] RivalColors = { new Color(.1f,.45f,.95f), new Color(.98f,.8f,.1f), new Color(.15f,.75f,.35f), new Color(.85f,.85f,.9f), new Color(.6f,.2f,.85f) };

    static readonly TrackTheme Desert = new DesertTheme();
    const string BestTimeKey = "RallyChampBest0";
    TrackTheme T => Desert;

    class Racer { public Transform t; public string name; public float progress, speed, skill, lane, finishTime; public bool player, done; }

    Transform world, car;
    Camera chase;
    Vector3[] path;
    float[] cum;
    float trackLength;
    readonly List<Racer> racers = new List<Racer>();
    Racer me;
    Material road, line, kerbA, kerbB, ground, foliage2, rock, rock2, glass, tyre;
    float steerS, camYaw;
    readonly List<Transform> crowdArms = new List<Transform>();
    readonly List<Transform> crowdFlags = new List<Transform>();
    readonly HashSet<string> pedalInput = new HashSet<string>();
    readonly Dictionary<int, string> touchInput = new Dictionary<int, string>();
    string mouseInput;
    Texture2D gasTexture, brakeTexture, dialTexture;
    bool Held(string key) { return pedalInput.Contains(key) || mouseInput == key || touchInput.ContainsValue(key); }
    float speed, heading, elapsed, countdown, best, vy;
    int lap, nearest;
    bool racing, paused, finished;
    GUIStyle title, label, small, button;
    AudioSource engine; AudioClip engineClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (SceneManager.GetActiveScene().name != "RallyMain" || FindObjectOfType<RallyGame>() != null) return;
        new GameObject("Rally Pioneers").AddComponent<RallyGame>();
    }
    void Start() { Application.targetFrameRate = 60; QualitySettings.vSyncCount = 1; BuildWorld(); }

    Material Mat(Color c, float gloss = .2f)
    {
        var sh = Shader.Find("Standard"); if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh); m.color = c; if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss); return m;
    }
    GameObject Shape(string n, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Transform parent = null, bool keepCollider = false)
    {
        var o = GameObject.CreatePrimitive(type); o.name = n;
        o.transform.SetParent(parent == null ? world : parent, false);
        o.transform.localPosition = pos; o.transform.localScale = scale;
        o.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Destroy(o.GetComponent<Collider>());
        return o;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t*t, t3 = t2*t;
        return .5f*((2*p1) + (-p0+p2)*t + (2*p0-5*p1+4*p2-p3)*t2 + (-p0+3*p1-3*p2+p3)*t3);
    }
    void BuildPath(Vector3[] cps)
    {
        const int per = 24; int n = cps.Length; path = new Vector3[n*per];
        for (int i = 0; i < n; i++) for (int s = 0; s < per; s++)
            path[i*per+s] = CatmullRom(cps[(i-1+n)%n], cps[i], cps[(i+1)%n], cps[(i+2)%n], s/(float)per);
        cum = new float[path.Length+1];
        for (int i = 0; i < path.Length; i++) cum[i+1] = cum[i] + Vector3.Distance(path[i], path[(i+1)%path.Length]);
        trackLength = cum[path.Length];
    }
    Vector3 At(float dist, out Vector3 fwd)
    {
        dist = Mathf.Repeat(dist, trackLength);
        int lo = 0, hi = path.Length;
        while (hi-lo > 1) { int mid = (lo+hi)/2; if (cum[mid] <= dist) lo = mid; else hi = mid; }
        Vector3 a = path[lo], b = path[(lo+1)%path.Length];
        fwd = (b-a).normalized;
        return Vector3.Lerp(a, b, (dist-cum[lo])/Mathf.Max(.001f, cum[lo+1]-cum[lo]));
    }

    void RoadMesh(string n, float inner, float outer, float lift, Material mat, bool collider)
    {
        int c = path.Length; var v = new Vector3[(c+1)*2]; var uv = new Vector2[v.Length]; var tri = new int[c*6];
        for (int i = 0; i <= c; i++)
        {
            Vector3 p = path[i%c], d = (path[(i+1)%c]-path[(i-1+c)%c]).normalized, side = Vector3.Cross(Vector3.up, d).normalized;
            v[i*2] = p + side*inner + Vector3.up*lift; v[i*2+1] = p + side*outer + Vector3.up*lift;
            float u = cum[Mathf.Min(i, c)]*.1f; uv[i*2] = new Vector2(0,u); uv[i*2+1] = new Vector2(1,u);
        }
        for (int i = 0; i < c; i++) { int k = i*6, a = i*2; tri[k]=a; tri[k+1]=a+2; tri[k+2]=a+1; tri[k+3]=a+1; tri[k+4]=a+2; tri[k+5]=a+3; }
        var mesh = new Mesh { name = n }; mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject(n); go.transform.SetParent(world, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // Whole footprint plus run-off clearance against every segment, not sparse footprint samples.
    bool StandClear(Transform stand)
    {
        float margin = Width*.5f+10;
        for (int i=0;i<path.Length;i++) {
            Vector3 a=stand.InverseTransformPoint(path[i]), b=stand.InverseTransformPoint(path[(i+1)%path.Length]);
            float lo=0, hi=1;
            if (ClipStandAxis(a.x,b.x-a.x,-48-margin,48+margin,ref lo,ref hi) &&
                ClipStandAxis(a.z,b.z-a.z,-23-margin,3+margin,ref lo,ref hi)) return false;
        }
        return true;
    }
    bool ClipStandAxis(float start,float delta,float min,float max,ref float lo,ref float hi)
    {
        if(Mathf.Abs(delta)<.00000001f) return start>=min && start<=max;
        float t1=(min-start)/delta,t2=(max-start)/delta;
        lo=Mathf.Max(lo,Mathf.Min(t1,t2)); hi=Mathf.Min(hi,Mathf.Max(t1,t2));
        return lo<=hi;
    }

    void BuildWorld()
    {
        if (world != null) Destroy(world.gameObject);
        racers.Clear(); crowdArms.Clear(); crowdFlags.Clear();
        world = new GameObject("Championship Track").transform;
        var th = T;
        BuildPath(th.Points);
        road = Mat(new Color(.16f,.17f,.19f), .35f); line = Mat(Color.white);
        kerbA = Mat(new Color(.9f,.12f,.1f)); kerbB = Mat(Color.white);
        ground = Mat(th.Ground);
        foliage2 = Mat(new Color(.18f,.42f,.15f));
        rock = Mat(th.RockA); rock2 = Mat(th.RockB);
        glass = Mat(new Color(.05f,.1f,.15f), .9f); tyre = Mat(new Color(.03f,.03f,.03f));

        Color sky = th.Sky;
        RenderSettings.fog = true; RenderSettings.fogColor = sky; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 120; RenderSettings.fogEndDistance = th.FogEnd;
        RenderSettings.ambientLight = th.Ambient;

        Shape("Landscape", PrimitiveType.Cube, new Vector3(0,-1.5f,0), new Vector3(2400,1,2400), ground);
        var mtn = Mat(th.Mountain);
        for (int i = 0; i < 60; i++) { float a = i/60f*Mathf.PI*2f, h = 120f + (i*37%9)*22f; var m = Shape("Mountain", PrimitiveType.Cube, new Vector3(Mathf.Cos(a)*900f, h*.4f, Mathf.Sin(a)*900f), new Vector3(220, h, 220), mtn); m.transform.rotation = Quaternion.Euler(0, i*17f, 45f); }
        RoadMesh("Road", -Width*.5f, Width*.5f, .05f, road, true);
        RoadMesh("Kerb L", -Width*.5f-.9f, -Width*.5f, .08f, kerbA, false);
        RoadMesh("Kerb R", Width*.5f, Width*.5f+.9f, .08f, kerbB, false);
        RoadMesh("Centre line", -.12f, .12f, .07f, line, false);
        var grass = Mat(new Color(.3f,.55f,.2f));
        RoadMesh("Verge L", -Width*.5f-7.6f, -Width*.5f-.9f, .03f, grass, false);
        RoadMesh("Verge R", Width*.5f+.9f, Width*.5f+7.6f, .03f, grass, false);
        Material[] boards = { Mat(new Color(.06f,.48f,.23f)), Mat(new Color(.89f,.25f,.1f)), Mat(new Color(.1f,.1f,.1f)), Mat(new Color(.11f,.31f,.85f)) };
        var steel = Mat(new Color(.72f,.75f,.77f), .8f); var fence = Mat(new Color(.75f,.78f,.8f, .35f));
        fence.SetFloat("_Mode", 3); fence.SetInt("_SrcBlend", 5); fence.SetInt("_DstBlend", 10); fence.SetInt("_ZWrite", 0); fence.EnableKeyword("_ALPHABLEND_ON"); fence.renderQueue = 3000;
        for (int i = 0; i < path.Length; i += 2)
        {
            Vector3 a = path[i], b = path[(i+2)%path.Length], d = (b-a).normalized, sd = Vector3.Cross(Vector3.up, d); float len = Vector3.Distance(a,b)+.1f; var rot = Quaternion.LookRotation(d);
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Vector3 m = (a+b)*.5f + sd*s2*(Width*.5f+7.5f);
                Shape("Sponsor board", PrimitiveType.Cube, m + Vector3.up*.55f, new Vector3(.15f,1.1f,len), boards[(i/10)%4]).transform.rotation = rot;
                Shape("Armco", PrimitiveType.Cube, m + sd*s2*.3f + Vector3.up*.45f, new Vector3(.1f,.3f,len), steel).transform.rotation = rot;
                Shape("Catch fence", PrimitiveType.Cube, m + Vector3.up*2.7f, new Vector3(.05f,3.2f,len), fence).transform.rotation = rot;
            }
        }
        // Seated 3D spectators, arm pivots and waving flags, facing the circuit.
        var standM = Mat(new Color(.47f,.51f,.55f));
        var roofM = Mat(new Color(.12f,.31f,.82f)); var pole = Mat(new Color(.87f,.87f,.87f));
        Material[] shirts = { Mat(new Color(1,.46f,.1f)), Mat(new Color(.94f,.32f,.12f)), Mat(Color.white), Mat(new Color(.15f,.53f,.87f)), Mat(new Color(1,.78f,.14f)) };
        Material[] skins = { Mat(new Color(.93f,.72f,.55f)), Mat(new Color(.74f,.5f,.34f)), Mat(new Color(.53f,.32f,.22f)) };
        Material[] flagM = { Mat(new Color(.35f,.66f,.9f)), Mat(new Color(1f,.48f,.1f)), Mat(Color.white) };
        float[] standsAt = { .02f, .3f, .55f, .8f };
        foreach (float fr in standsAt) for (int sg = -1; sg <= 1; sg += 2)
        {
            if (fr != .02f && sg == 1) continue;
            Vector3 sf; Vector3 sp = At(trackLength*fr, out sf); Vector3 sd = Vector3.Cross(Vector3.up, sf).normalized;
            var g = new GameObject("Grandstand").transform; g.SetParent(world); g.position = sp + sd*sg*(Width*.5f+14f) + Vector3.down*.5f; g.rotation = Quaternion.LookRotation(-sd*sg);
            bool safeStand = false;
            for (int attempt = 0; attempt < 128; attempt++) {
                if (StandClear(g)) { safeStand = true; break; }
                g.position += sd*sg*8;
            }
            if (!safeStand) { Destroy(g.gameObject); continue; }
            for (int r = 0; r < 8; r++) {
                Shape("Tier", PrimitiveType.Cube, new Vector3(0, (1.6f+r*1.6f)*.5f, -r*2.4f), new Vector3(90, 1.6f+r*1.6f, 2.5f), standM, g);
                for (int c2 = 0; c2 < 45; c2++) {
                    var fan = new GameObject("Seated cheering spectator").transform; fan.SetParent(g, false); fan.localPosition = new Vector3(-44+c2*2f+(r%2)*.5f, 1.6f+r*1.6f, -r*2.4f);
                    var skin = skins[(c2+r)%skins.Length];
                    Shape("Shirt", PrimitiveType.Cube, new Vector3(0,.42f,0), new Vector3(.55f,.65f,.35f), shirts[(c2*7+r*3)%shirts.Length], fan);
                    Shape("Head", PrimitiveType.Sphere, new Vector3(0,.94f,0), new Vector3(.34f,.34f,.34f), skin, fan);
                    for (int j = -1; j <= 1; j += 2) {
                        Shape("Seated leg", PrimitiveType.Cube, new Vector3(j*.16f,.12f,.2f), new Vector3(.18f,.19f,.6f), tyre, fan);
                        var arm = new GameObject("Waving arm pivot").transform; arm.SetParent(fan, false); arm.localPosition = new Vector3(j*.33f,.72f,0); crowdArms.Add(arm);
                        Shape("Arm", PrimitiveType.Cube, new Vector3(0,.24f,0), new Vector3(.13f,.52f,.13f), skin, arm);
                    }
                }
            }
            Shape("Roof", PrimitiveType.Cube, new Vector3(0,17,-10), new Vector3(94,.6f,22), roofM, g);
            for (float x = -45; x <= 45; x += 6) { Shape("Flag pole", PrimitiveType.Cylinder, new Vector3(x,23,-19.5f), new Vector3(.15f,3,.15f), pole, g); var fl = Shape("Waving flag", PrimitiveType.Cube, new Vector3(x+1.2f,25,-19.5f), new Vector3(2.4f,1.4f,.05f), flagM[((int)(x/6)+99)%3], g); crowdFlags.Add(fl.transform); }
        }

        var rnd = new System.Random(th.Seed);
        for (int i = 0; i < path.Length; i += 3)
        {
            Vector3 p = path[i], d = (path[(i+1)%path.Length]-p).normalized, side = Vector3.Cross(Vector3.up, d);
            // Embankment under elevated road
            // Continuous road/verges only: no protruding embankment blocks on asphalt.
            for (int s = -1; s <= 1; s += 2)
            {
                if (rnd.NextDouble() > .55) continue;
                Vector3 q = p + side*s*(Width*.5f + 16f + (float)rnd.NextDouble()*40f); q.y = 0;
                if (DistToTrack(q) < Width*.5f + 12f) continue;
                th.PlaceScenery(new SceneryContext { spot = q, trackPoint = p, side = side, sideSign = s, rnd = rnd,
                    shape = (n, t, pos, sc, m) => Shape(n, t, pos, sc, m), foliage2 = foliage2, rock = rock, rock2 = rock2 });
            }
        }
        // Start gantry + grid
        Vector3 f0; Vector3 s0 = At(0, out f0); Quaternion r0 = Quaternion.LookRotation(f0); Vector3 sideS = Vector3.Cross(Vector3.up, f0);
        for (int x = 0; x < 14; x++) for (int z = 0; z < 2; z++)
            Shape("Checker", PrimitiveType.Cube, s0 + r0*new Vector3((x-6.5f)*.86f, .12f, z*.85f), new Vector3(.86f,.04f,.85f), (x+z)%2==0?kerbB:tyre).transform.rotation = r0;
        for (int s = -1; s <= 1; s += 2) Shape("Gantry post", PrimitiveType.Cube, s0 + sideS*s*(Width*.5f+1.5f) + Vector3.up*4, new Vector3(.6f,8,.6f), tyre);
        Shape("Gantry", PrimitiveType.Cube, s0 + Vector3.up*8, new Vector3(Width+4, 1.4f, .6f), kerbA).transform.rotation = r0;

        var sun = new GameObject("Sun"); sun.transform.SetParent(world); sun.transform.rotation = Quaternion.Euler(th.SunAngles);
        var light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = th.SunIntensity;
        light.color = th.SunColor; light.shadows = LightShadows.Soft;

        me = new Racer { player = true, name = "YOU" }; me.t = MakeCar("Player Car", new Color(.95f,.22f,.08f)); car = me.t; racers.Add(me);
        for (int i = 0; i < Rivals; i++)
            racers.Add(new Racer { name = RivalNames[i], t = MakeCar("AI " + RivalNames[i], RivalColors[i]), skill = .86f + i*.025f, lane = ((i%2)*2-1)*2.8f });

        if (chase == null)
        {
            var c = new GameObject("Chase Camera"); chase = c.AddComponent<Camera>(); c.AddComponent<AudioListener>(); chase.fieldOfView = 62; chase.farClipPlane = 2000;
            engine = c.AddComponent<AudioSource>(); engine.loop = true; engine.volume = .08f;
            float[] smp = new float[22050]; for (int i = 0; i < smp.Length; i++) smp[i] = Mathf.Sin(i*2*Mathf.PI*80/22050f)*.5f + Mathf.Sin(i*2*Mathf.PI*160/22050f)*.25f + Mathf.Sin(i*2*Mathf.PI*240/22050f)*.1f;
            engineClip = AudioClip.Create("Engine", smp.Length, 1, 22050, false); engineClip.SetData(smp, 0); engine.clip = engineClip;
        }
        chase.backgroundColor = sky; chase.clearFlags = CameraClearFlags.SolidColor;
        ResetRace(false);
    }

    Transform MakeCar(string n, Color col)
    {
        var t = new GameObject(n).transform; t.SetParent(world);
        var paint = Mat(col, .75f); var carbon = Mat(new Color(.1f,.1f,.12f), .6f); var rim = Mat(new Color(.6f,.63f,.66f), .8f);
        Shape("Floor", PrimitiveType.Cube, new Vector3(0,.18f,-.1f), new Vector3(1.5f,.06f,4f), carbon, t);
        Shape("Nose cone", PrimitiveType.Capsule, new Vector3(0,.38f,1.75f), new Vector3(.4f,1.15f,.3f), paint, t).transform.localRotation = Quaternion.Euler(92,0,0);
        Shape("Monocoque", PrimitiveType.Cube, new Vector3(0,.48f,.15f), new Vector3(.7f,.42f,1.6f), paint, t);
        for (int s = -1; s <= 1; s += 2) {
            Shape("Sidepod", PrimitiveType.Capsule, new Vector3(s*.62f,.42f,-.45f), new Vector3(.62f,.9f,.48f), paint, t).transform.localRotation = Quaternion.Euler(90,0,0);
            Shape("Sidepod inlet", PrimitiveType.Cube, new Vector3(s*.62f,.45f,.42f), new Vector3(.42f,.26f,.05f), tyre, t);
            Shape("Front endplate", PrimitiveType.Cube, new Vector3(s*1.15f,.26f,2.9f), new Vector3(.04f,.24f,.6f), paint, t);
            Shape("Rear endplate", PrimitiveType.Cube, new Vector3(s*.86f,.85f,-2.1f), new Vector3(.04f,.65f,.6f), paint, t);
        }
        Shape("Engine cover", PrimitiveType.Capsule, new Vector3(0,.6f,-1.15f), new Vector3(.5f,.85f,.45f), paint, t).transform.localRotation = Quaternion.Euler(90,0,0);
        Shape("Airbox", PrimitiveType.Cube, new Vector3(0,.98f,-.62f), new Vector3(.34f,.36f,.5f), paint, t);
        Shape("Airbox intake", PrimitiveType.Cube, new Vector3(0,1.06f,-.36f), new Vector3(.2f,.16f,.04f), tyre, t);
        Shape("Shark fin", PrimitiveType.Cube, new Vector3(0,.85f,-1.3f), new Vector3(.04f,.3f,.9f), Mat(Color.white), t);
        Shape("Front wing", PrimitiveType.Cube, new Vector3(0,.16f,2.95f), new Vector3(2.3f,.04f,.5f), carbon, t);
        Shape("Front flap 1", PrimitiveType.Cube, new Vector3(0,.24f,2.82f), new Vector3(2.1f,.03f,.25f), paint, t);
        Shape("Front flap 2", PrimitiveType.Cube, new Vector3(0,.3f,2.72f), new Vector3(1.9f,.03f,.15f), paint, t);
        Shape("Rear wing", PrimitiveType.Cube, new Vector3(0,1.12f,-2.15f), new Vector3(1.7f,.05f,.42f), paint, t);
        Shape("Rear flap", PrimitiveType.Cube, new Vector3(0,1f,-2.05f), new Vector3(1.7f,.04f,.22f), carbon, t);
        Shape("Beam wing", PrimitiveType.Cube, new Vector3(0,.55f,-2.1f), new Vector3(1.3f,.04f,.2f), carbon, t);
        Shape("Wing pylon", PrimitiveType.Cube, new Vector3(0,.82f,-2.05f), new Vector3(.08f,.5f,.12f), carbon, t);
        // Halo: two side bars + centre pillar
        for (int s = -1; s <= 1; s += 2) Shape("Halo", PrimitiveType.Cube, new Vector3(s*.3f,.9f,-.1f), new Vector3(.06f,.06f,.6f), carbon, t).transform.localRotation = Quaternion.Euler(-8, s*-12, 0);
        Shape("Halo pillar", PrimitiveType.Cube, new Vector3(0,.82f,.28f), new Vector3(.06f,.24f,.06f), carbon, t);
        // Driver with helmet and visor
        Shape("Driver shoulders", PrimitiveType.Cube, new Vector3(0,.7f,-.1f), new Vector3(.5f,.18f,.35f), tyre, t);
        Shape("Helmet", PrimitiveType.Sphere, new Vector3(0,.86f,-.08f), new Vector3(.42f,.42f,.42f), Mat(Color.white, .8f), t);
        Shape("Helmet stripe", PrimitiveType.Sphere, new Vector3(0,.88f,-.08f), new Vector3(.44f,.15f,.44f), paint, t);
        Shape("Visor", PrimitiveType.Cube, new Vector3(0,.87f,.11f), new Vector3(.28f,.1f,.06f), glass, t);
        // Wide open-wheel tyres (rears wider)
        for (int s = -1; s <= 1; s += 2) for (int z = 0; z < 2; z++) {
            float zz = z == 0 ? 1.6f : -1.55f, w = z == 0 ? .38f : .5f, r = z == 0 ? .36f : .4f;
            Shape("Tyre", PrimitiveType.Cylinder, new Vector3(s*1f, r, zz), new Vector3(r*2, w*.5f, r*2), tyre, t).transform.localRotation = Quaternion.Euler(0,0,90);
            Shape("Rim", PrimitiveType.Cylinder, new Vector3(s*(1f+w*.5f), r, zz), new Vector3(r*1.2f, .01f, r*1.2f), rim, t).transform.localRotation = Quaternion.Euler(0,0,90);
            Shape("Suspension", PrimitiveType.Cube, new Vector3(s*.6f, r+.05f, zz), new Vector3(.75f,.03f,.06f), carbon, t);
        }
        return t;
    }

    float DistToTrack(Vector3 q)
    {
        float best = float.MaxValue; q.y = 0;
        for (int i = 0; i < path.Length; i += 2) { Vector3 p = path[i]; p.y = 0; best = Mathf.Min(best, (p-q).sqrMagnitude); }
        return Mathf.Sqrt(best);
    }
    // Nearest path index around a hint (fast local search) -> distance along track & lateral offset.
    float Project(Vector3 pos, ref int hint, out float lateral, out float height)
    {
        int c = path.Length, bestI = hint; float bestD = float.MaxValue;
        for (int k = -40; k <= 40; k++) { int i = (hint+k+c)%c; Vector3 p = path[i]; float d = (p.x-pos.x)*(p.x-pos.x)+(p.z-pos.z)*(p.z-pos.z); if (d < bestD) { bestD = d; bestI = i; } }
        hint = bestI;
        Vector3 a = path[bestI], b = path[(bestI+1)%c], ab = b-a; ab.y = 0; Vector3 ap = pos-a; ap.y = 0;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab)/Mathf.Max(.001f, ab.sqrMagnitude));
        Vector3 on = Vector3.Lerp(a, b, t); Vector3 side = Vector3.Cross(Vector3.up, ab.normalized);
        lateral = Vector3.Dot(pos-on, side); height = on.y;
        return cum[bestI] + t*(cum[bestI+1]-cum[bestI]);
    }

    void ResetRace(bool go)
    {
        pedalInput.Clear(); touchInput.Clear(); mouseInput = null; steerS = 0; speed = 0; elapsed = 0; lap = 1; countdown = 3.5f; paused = false; finished = false; racing = go; nearest = 0; vy = 0;
        for (int i = 0; i < racers.Count; i++)
        {
            var r = racers[i]; r.done = false; r.finishTime = 0; r.speed = 0;
            float back = -8f - i*7f; r.progress = back;
            Vector3 f; Vector3 p = At(back, out f); Vector3 side = Vector3.Cross(Vector3.up, f);
            float lat = (i%2==0 ? -4.5f : 4.5f); if (!r.player) r.lane = lat;
            r.t.position = p + side*lat; r.t.rotation = Quaternion.LookRotation(f);
        }
        heading = car.eulerAngles.y; camYaw = heading; nearest = path.Length - 4;
        best = PlayerPrefs.GetFloat(BestTimeKey, 0);
        chase.transform.position = car.position - car.forward*9 + Vector3.up*4; chase.transform.LookAt(car.position + Vector3.up);
        if (engine != null) { if (go) engine.Play(); else engine.Stop(); }
    }
    void Respawn()
    {
        Vector3 f; Vector3 p = At(me.progress, out f);
        car.position = p + Vector3.up*.3f; heading = Quaternion.LookRotation(f).eulerAngles.y; speed = 0;
    }

    void ResolveFence(ref Vector3 pos, Vector3 fwd)
    {
        float lat,h; Project(pos,ref nearest,out lat,out h);
        Vector3 dir=path[(nearest+1)%path.Length]-path[nearest]; dir.y=0; Vector3 side=Vector3.Cross(Vector3.up,dir.normalized);
        float dot=Vector3.Dot(fwd,side), extent=3.1f*Mathf.Abs(dot)+1.3f*Mathf.Sqrt(Mathf.Max(0,1-dot*dot)), limit=Width*.5f+7.5f-extent-.15f;
        if(Mathf.Abs(lat)>limit) { Vector3 outward=side*Mathf.Sign(lat); pos-=outward*(Mathf.Abs(lat)-limit); float d=Vector3.Dot(fwd,outward); if(speed*d>0) speed*=Mathf.Max(0,1-Mathf.Abs(d)*1.6f); }
    }
    void PollPedals()
    {
        if(!racing || paused || finished) { mouseInput=null; touchInput.Clear(); pedalInput.Clear(); return; }
        System.Func<Vector2,string> hit = p => p.y>=572 && p.y<=700 && p.x>=1108 && p.x<=1182?"Gas":p.y>=592 && p.y<=700 && p.x>=1022 && p.x<=1096?"Brake":p.y>=628 && p.x>=24 && p.x<96?"Left":p.y>=628 && p.x>=108 && p.x<180?"Right":null;
        Vector2 mouse=new Vector2(Input.mousePosition.x/Screen.width*1280,(Screen.height-Input.mousePosition.y)/Screen.height*720);
        if(Input.GetMouseButtonDown(0)) mouseInput=hit(mouse); if(!Input.GetMouseButton(0)) mouseInput=null;
        foreach(var touch in Input.touches) { Vector2 p=new Vector2(touch.position.x/Screen.width*1280,(Screen.height-touch.position.y)/Screen.height*720); if(touch.phase==TouchPhase.Began){string key=hit(p); if(key!=null) touchInput[touch.fingerId]=key;} if(touch.phase==TouchPhase.Ended || touch.phase==TouchPhase.Canceled) touchInput.Remove(touch.fingerId); }
    }
    void OnApplicationFocus(bool focus) { if(!focus){mouseInput=null;touchInput.Clear();pedalInput.Clear();} }
    void Update()
    {
        PollPedals();
        for(int i=0;i<crowdArms.Count;i++) crowdArms[i].localRotation=Quaternion.Euler(0,0,(i%2==0?-1:1)*(28+Mathf.Sin(Time.time*3+i*.73f)*28));
        for(int i=0;i<crowdFlags.Count;i++) crowdFlags[i].localRotation=Quaternion.Euler(0,Mathf.Sin(Time.time*3+i)*14,Mathf.Sin(Time.time*4+i)*5);
        if (Input.GetKeyDown(KeyCode.Escape) && racing && !finished) { paused = !paused; if (paused) engine.Pause(); else engine.UnPause(); }
        if (Input.GetKeyDown(KeyCode.R) && racing) ResetRace(true);
        if (Input.GetKeyDown(KeyCode.B) && racing && !paused && !finished) Respawn();
        if (!racing || paused) return;
        if (countdown > 0) { countdown -= Time.deltaTime; return; }
        float dt = Mathf.Min(Time.deltaTime, .05f);
        if (!finished) elapsed += dt;

        // --- Player physics: held reverse, moderate grass drag, full-footprint fence stop.
        // Explicit semantic inputs avoid project axis overrides; Unity +yaw is chase-view right.
        float throttle = finished ? 0 : ((Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0));
        float steer = finished ? 0 : ((Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0));
        if (!finished) { if (Held("Gas")) throttle += 1; if (Held("Brake")) throttle -= 1; if (Held("Left")) steer -= 1; if (Held("Right")) steer += 1; }
        throttle = Mathf.Clamp(throttle,-1,1); steer = Mathf.Clamp(steer,-1,1);
        steerS += (steer-steerS)*(1-Mathf.Exp(-5*dt)); steer = steerS;
        float lateral, h; float dist = Project(car.position, ref nearest, out lateral, out h);
        bool offroad = Mathf.Abs(lateral) > Width*.5f+.9f;
        float top=72f*(offroad ? .6f : 1f);
        if(throttle>0) speed+=20f*1.6f*(1-Mathf.Max(0,speed)/top)*dt;
        else if(throttle<0) speed-=(speed>0?45:12*(1-Mathf.Min(1,-speed/14)))*dt;
        speed=Mathf.MoveTowards(speed,0,(Input.GetKey(KeyCode.Space)?55:offroad?6:4)*dt);
        speed=Mathf.Clamp(speed,-14,top);
        float grip=1-.45f*Mathf.Min(1,Mathf.Abs(speed)/90);
        heading+=steer*80*grip*Mathf.Clamp(speed/14,-1,1)*dt;
        Vector3 fwd=Quaternion.Euler(0,heading,0)*Vector3.forward;
        Vector3 pos=car.position+fwd*speed*dt;
        ResolveFence(ref pos,fwd); float nLat,nH; Project(pos,ref nearest,out nLat,out nH); pos.y=nH+.05f; vy=0;
        Vector3 ahead; At(dist+2,out ahead); float pitch=-Mathf.Asin(Mathf.Clamp(ahead.y,-1,1))*Mathf.Rad2Deg;
        car.position = pos; car.rotation = Quaternion.Euler(pitch, heading, -steer*Mathf.Min(1f, Mathf.Abs(speed)/60f)*3f);

        // Lap / progress tracking (monotonic; ignores shortcuts backwards)
        float prevLapPos = Mathf.Repeat(me.progress, trackLength);
        float delta = Mathf.DeltaAngle(prevLapPos/trackLength*360f, dist/trackLength*360f)/360f*trackLength;
        if (Mathf.Abs(delta) < 60f) me.progress += delta;
        int newLap = Mathf.FloorToInt(me.progress/trackLength) + 1;
        if (!finished && newLap > Laps) { finished = true; me.done = true; me.finishTime = elapsed; if (best == 0 || elapsed < best) { best = elapsed; PlayerPrefs.SetFloat(BestTimeKey, best); PlayerPrefs.Save(); } }
        lap = Mathf.Clamp(newLap, 1, Laps);

        // --- AI rivals: follow spline, brake for curvature, rubber-band
        foreach (var r in racers)
        {
            if (r.player) continue;
            Vector3 f1, f2; At(r.progress, out f1); At(r.progress + 30f, out f2);
            float curve = Vector3.Angle(new Vector3(f1.x,0,f1.z), new Vector3(f2.x,0,f2.z));
            float target = Mathf.Lerp(74f, 34f, Mathf.Clamp01(curve/70f))*r.skill;
            float gap = me.progress - r.progress; target *= 1f + Mathf.Clamp(gap/400f, -.12f, .12f);
            if (r.done) target = 20f;
            r.speed = Mathf.MoveTowards(r.speed, target, 18f*dt);
            r.progress += r.speed*dt;
            if (!r.done && r.progress >= trackLength*Laps) { r.done = true; r.finishTime = elapsed; }
            Vector3 f; Vector3 p = At(r.progress, out f); Vector3 side = Vector3.Cross(Vector3.up, f).normalized;
            r.lane = Mathf.MoveTowards(r.lane, Mathf.Sin(r.progress*.01f + r.skill*20f)*3f, dt*1.5f);
            // simple collision avoidance with player
            Vector3 toMe = car.position - (p + side*r.lane);
            if (toMe.magnitude < 4f) r.lane -= Mathf.Sign(Vector3.Dot(toMe, side))*dt*6f;
            r.lane = Mathf.Clamp(r.lane, -Width*.4f, Width*.4f);
            r.t.position = p + side*r.lane + Vector3.up*.05f;
            r.t.rotation = Quaternion.LookRotation(f);
        }
        // Player vs AI bump
        foreach (var r in racers) if (!r.player) { Vector3 d = car.position - r.t.position; d.y = 0; if (d.magnitude < 2.6f) { car.position += d.normalized*(2.6f - d.magnitude); speed *= .97f; } }

        Vector3 safe = car.position; ResolveFence(ref safe, Quaternion.Euler(0,heading,0)*Vector3.forward); float safeLat,safeH; Project(safe,ref nearest,out safeLat,out safeH); safe.y=safeH+.05f; car.position=safe;
        engine.pitch = .55f + Mathf.Abs(speed)*.025f;
    }
    void LateUpdate()
    {
        if (car == null) return;
        // Camera trails the car's yaw so the nose visibly swings toward the steer direction (matches web preview).
        camYaw = Mathf.LerpAngle(camYaw, heading, 1 - Mathf.Exp(-4*Time.deltaTime));
        Vector3 back = Quaternion.Euler(0, camYaw, 0) * Vector3.forward;
        chase.transform.position = car.position - back*9f + Vector3.up*3.8f;
        chase.transform.rotation = Quaternion.LookRotation(car.position + back*6f + Vector3.up - chase.transform.position);
        chase.fieldOfView = Mathf.Lerp(chase.fieldOfView, 60 + Mathf.Abs(speed)*.22f, Time.deltaTime*3);
    }

    int Position() { int p = 1; foreach (var r in racers) if (!r.player && Score(r) > Score(me)) p++; return p; }
    float Score(Racer r) => r.done ? 1e7f - r.finishTime : r.progress;
    string Clock(float t) => string.Format("{0:00}:{1:00.00}", Mathf.FloorToInt(t/60), t%60);
    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold }; title.normal.textColor = Color.white;
        label = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold }; label.normal.textColor = Color.white;
        small = new GUIStyle(GUI.skin.label) { fontSize = 16 }; small.normal.textColor = Color.white;
        button = new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 46 };
    }
    Texture2D PedalTexture(bool gas)
    {
        var tex=new Texture2D(64,110,TextureFormat.RGBA32,false);
        for(int y=0;y<110;y++)for(int x=0;x<64;x++) { float xx=(x-32f)/(gas?22:24), yy=(y-55f)/51; bool inside=xx*xx+yy*yy<1; Color c=inside?new Color(.78f,.8f,.82f):Color.clear; if(inside && y>20 && y<90 && y%14<5 && x>17 && x<47)c=new Color(.12f,.14f,.16f); tex.SetPixel(x,y,c); } tex.Apply(); return tex;
    }
    Texture2D DialTexture()
    {
        var tex=new Texture2D(160,160,TextureFormat.RGBA32,false);
        for(int y=0;y<160;y++)for(int x=0;x<160;x++){float dx=x-80,dy=y-80,r=Mathf.Sqrt(dx*dx+dy*dy),a=Mathf.Atan2(dy,dx)*Mathf.Rad2Deg; bool arc=(a>=-40 || a<=-140); tex.SetPixel(x,y,r<78?new Color(.09f,.11f,.13f,.85f):Color.clear);if(r>67 && r<72 && arc)tex.SetPixel(x,y,new Color(.45f,.48f,.5f));} tex.Apply(); return tex;
    }
    void DrawDrivingHud()
    {
        if(gasTexture==null){gasTexture=PedalTexture(true);brakeTexture=PedalTexture(false);dialTexture=DialTexture();}
        GUI.DrawTexture(new Rect(1090,16,160,160),dialTexture);
        var center=new Vector2(1170,96);float amount=Mathf.Clamp01(Mathf.Abs(speed)*3.6f/380);
        Color old=GUI.color;GUI.color=new Color(1,.87f,.1f);
        for(int i=0;i<=amount*60;i++){float a=(140-i*260/60f)*Mathf.Deg2Rad;GUI.DrawTexture(new Rect(center.x+70*Mathf.Cos(a)-2,center.y-70*Mathf.Sin(a)-2,4,4),Texture2D.whiteTexture);}
        GUI.color=old; GUI.Label(new Rect(1119,52,105,42), Mathf.RoundToInt(Mathf.Abs(speed)*3.6f).ToString("000"),title);GUI.Label(new Rect(1142,105,70,24),"KM/H",small);GUI.Label(new Rect(1160,126,40,26),speed<-.1f?"R":Mathf.Abs(speed)<.2f?"N":Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(speed)*3.6f/45),1,8).ToString(),small);
        GUI.Box(new Rect(24,628,72,72),"");GUI.Label(new Rect(39,634,60,60),"←",title);
        GUI.Box(new Rect(108,628,72,72),"");GUI.Label(new Rect(122,634,60,60),"→",title);
        GUI.Box(new Rect(1022,592,74,108),"");GUI.DrawTexture(new Rect(1030,598,58,94),brakeTexture);GUI.Box(new Rect(1108,572,74,128),"");GUI.DrawTexture(new Rect(1116,580,58,110),gasTexture);
    }
    void OnGUI()
    {
        Styles(); GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width/1280f, Screen.height/720f, 1));
        string trackName = T.DisplayName;
        if (!racing)
        {
            GUI.Box(new Rect(45,80,600,560), "");
            GUI.Label(new Rect(75,105,560,60), "RALLY PIONEERS", title);
            GUI.Label(new Rect(77,165,540,30), "DESERT CHAMPIONSHIP", small);
            GUI.Label(new Rect(77,215,540,40), "6 cars · 3 laps · " + Mathf.RoundToInt(trackLength) + " m circuit", label);
            GUI.Label(new Rect(77,280,520,46), "TRACK: " + trackName, label);
            if (GUI.Button(new Rect(77,345,520,46), "START RACE", button)) ResetRace(true);
            GUI.Label(new Rect(77,420,520,120), "W/S or ↑/↓: throttle / brake-reverse   A/D or ←/→: steer\nSpace: handbrake   B: recover to track\nR: restart   Esc: pause", small);
            GUI.Label(new Rect(77,540,520,60), "Best time on this track: " + (best > 0 ? Clock(best) : "--:--"), small);
            return;
        }
        GUI.Box(new Rect(24,20,1010,92), "");
        GUI.Label(new Rect(45,30,200,34), "POS " + Position() + " / " + racers.Count, label);
        GUI.Label(new Rect(260,30,200,34), "LAP " + lap + " / " + Laps, label);
        GUI.Label(new Rect(470,30,260,34), "TIME " + Clock(elapsed), label);
        GUI.Label(new Rect(760,30,260,34), "BEST " + (best > 0 ? Clock(best) : "--:--"), label);
        DrawDrivingHud();
        GUI.Label(new Rect(45,72,1000,30), trackName, small);

        // Minimap
        GUI.Box(new Rect(24,130,180,180), "");
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in path) { minX = Mathf.Min(minX,p.x); maxX = Mathf.Max(maxX,p.x); minZ = Mathf.Min(minZ,p.z); maxZ = Mathf.Max(maxZ,p.z); }
        float sc = 160f/Mathf.Max(maxX-minX, maxZ-minZ);
        System.Func<Vector3, Vector2> M = p => new Vector2(34 + (p.x-minX)*sc, 300 - (p.z-minZ)*sc);
        for (int i = 0; i < path.Length; i += 4) { var m = M(path[i]); GUI.Label(new Rect(m.x-3, m.y-8, 10, 14), "·", small); }
        foreach (var r in racers) { var m = M(r.t.position); var prevC = GUI.color; GUI.color = r.player ? new Color(1f,.35f,.12f) : RivalColors[racers.IndexOf(r)-1]; GUI.Label(new Rect(m.x-6, m.y-11, 20, 22), "●", small); GUI.color = prevC; }

        if (countdown > 0 && !paused) GUI.Label(new Rect(600,280,200,80), countdown > .5f ? Mathf.CeilToInt(countdown-.5f).ToString() : "GO!", title);
        if (paused) {
            GUI.Box(new Rect(390,200,500,300), "");
            GUI.Label(new Rect(425,225,440,60), "PAUSED", title);
            if (GUI.Button(new Rect(425,320,430,46), "RESUME", button)) { paused = false; engine.UnPause(); }
            if (GUI.Button(new Rect(425,385,430,46), "MAIN MENU", button)) { racing = false; paused = false; engine.Stop(); }
        }
        if (finished)
        {
            GUI.Box(new Rect(360,130,560,470), "");
            GUI.Label(new Rect(395,150,500,60), "RESULTS — P" + Position(), title);
            var order = new List<Racer>(racers); order.Sort((a,b) => Score(b).CompareTo(Score(a)));
            for (int i = 0; i < order.Count; i++)
                GUI.Label(new Rect(400,220+i*34,480,30), (i+1) + ".  " + order[i].name.PadRight(14) + (order[i].done ? Clock(order[i].finishTime) : "racing…"), label);
            if (GUI.Button(new Rect(395,450,490,46), "RACE AGAIN", button)) ResetRace(true);
            if (GUI.Button(new Rect(395,515,490,46), "MAIN MENU", button)) { racing = false; engine.Stop(); }
        }
    }
    void OnDestroy() { if (engineClip != null) Destroy(engineClip); }
}
