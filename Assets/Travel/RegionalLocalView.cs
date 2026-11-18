using UnityEngine;

// One map unit is one source-art pixel. Physical props, the walking model and
// interaction distances all use the same bottom-centre foot position.
public sealed class RegionalLocalView
{
    readonly Camera camera;
    MapPlace visible = MapPlace.World;
    GameObject stage;
    Transform contact;
    SpriteRenderer traveler;
    Transform brigand;
    GameObject lookout;
    string lastAmbushData;
    Vector2 lastPosition;

    public RegionalLocalView(Camera camera) => this.camera = camera;
    public SpriteRenderer Traveler => traveler;
    public bool HasBrigands => brigand && brigand.gameObject.activeSelf;

    static Vector2 ToWorld(Vector2 point) => new Vector2((point.x - 500f) / 32f, (320f - point.y) / 32f);

    public bool Obstructed(Vector2 point) => stage && Physics2D.OverlapCircle(ToWorld(point), .13f);
    public bool NearContact(TravelModel travel) => contact &&
        Vector2.Distance(ToWorld(travel.Position), contact.position) <= 1.8f;
    public bool NearBrigand(TravelModel travel) => HasBrigands && DemoRun.Sensed &&
        Vector2.Distance(ToWorld(travel.Position), brigand.position) <= 2.5f;

    public void Show(TravelModel travel)
    {
        if (visible != travel.Place)
        {
            visible = travel.Place;
            if (stage)
            {
                stage.SetActive(false);
                Object.Destroy(stage);
                stage = null;
            }
            if (visible != MapPlace.Pass && visible != MapPlace.Sect) return;
            bool sect = visible == MapPlace.Sect;
            stage = Object.Instantiate(Resources.Load<GameObject>(sect ? "Regions/QixiaGate" : "Regions/SongfengPass"));
            traveler = stage.transform.Find("行者").GetComponent<SpriteRenderer>();
            contact = stage.transform.Find(sect ? "执事" : "石隙药圃");
            brigand = stage.transform.Find("灰衣客");
            if (brigand)
            {
                lookout = stage.transform.Find("望路人").gameObject;
                lastAmbushData = null;
                var sprite = lookout.GetComponent<SpriteRenderer>();
                if (BrigandTerms.CutsOffRoad) lookout.transform.position = ToWorld(new Vector2(699, 329));
                sprite.sortingOrder = 1000 - Mathf.RoundToInt(lookout.transform.position.y * 10f);
                sprite.flipX = !BrigandTerms.CutsOffRoad;
            }
            lastPosition = travel.Position;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = sect ? new Color(.66f, .72f, .59f) : new Color(.54f, .63f, .54f);
            camera.transform.rotation = Quaternion.identity;
            Physics2D.SyncTransforms();
        }
        if (!stage) return;
        if (brigand && lastAmbushData != DemoRun.AmbushData)
        {
            lastAmbushData = DemoRun.AmbushData;
            bool present = !BrigandTerms.Load().Settled;
            brigand.gameObject.SetActive(present);
            lookout.SetActive(present);
        }
        Vector2 feet = ToWorld(travel.Position);
        traveler.transform.position = feet;
        traveler.sortingOrder = 1000 - Mathf.RoundToInt(feet.y * 10f);
        if (Mathf.Abs(travel.Position.x - lastPosition.x) > .01f) traveler.flipX = travel.Position.x < lastPosition.x;
        lastPosition = travel.Position;
        float halfHeight = Mathf.Min(5.8f, 15.6f / camera.aspect);
        float halfWidth = halfHeight * camera.aspect;
        camera.orthographicSize = halfHeight;
        camera.transform.position = new Vector3(Mathf.Clamp(feet.x, -15.625f + halfWidth, 15.625f - halfWidth),
            Mathf.Clamp(feet.y + .7f, -10f + halfHeight, 10f - halfHeight), -15f);
    }
}
