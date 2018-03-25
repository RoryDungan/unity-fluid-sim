using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using Unity.Mathematics;

public class FluidSim : MonoBehaviour
{
    [SerializeField]
    private int particleCount = 10000;

    [SerializeField]
    private float particlePlacementRadius = 10f;

    [SerializeField]
    private Vector3 acceleration = new Vector3(0.0002f, 0.0001f, 0.0002f);

    [SerializeField]
    private Vector3 accelerationMod = new Vector3(0.0001f, 0.0001f, 0.0001f);

    private NativeArray<Vector3> velocities;
    private TransformAccessArray transformAccessArray;

    private PositionUpdateJob job;
    private AccelerationJob accelerationJob;

    private JobHandle positionJobHandle;
    private JobHandle accelJobHandle;

    private GameObject[] objects;
    private Transform[] transforms;
    private Renderer[] renderers;

    private struct PositionUpdateJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<Vector3> velocity; // the velocities from the AccelerationJob

        public float deltaTime;

        public void Execute(int i, TransformAccess transform)
        {
            transform.position += velocity[i] * deltaTime;
        }
    }

    private struct AccelerationJob : IJobParallelFor
    {
        public NativeArray<Vector3> velocity;

        public Vector3 acceleration;
        public Vector3 accelerationMod;

        public float deltaTime;

        public void Execute(int i)
        {
            // here, i'm intentionally using the index to affect acceleration (it looks cool),
            // but generating velocities probably wouldn't be tied to index normally.
            velocity[i] += (acceleration + i * accelerationMod) * deltaTime;
        }
    }

    private void Awake()
    {
        objects = new GameObject[particleCount];
        transforms = new Transform[particleCount];
        renderers = new Renderer[particleCount];
    }

    private void Start()
    {
        velocities = new NativeArray<Vector3>(particleCount, Allocator.Persistent);
        objects = PlaceRandomSpheres(particleCount, particlePlacementRadius);

        for (var i = 0; i < particleCount; i++)
        {
            var obj = objects[i];
            transforms[i] = obj.transform;
            renderers[i] = obj.GetComponent<Renderer>();
        }

        transformAccessArray = new TransformAccessArray(transforms);
    }

    private void Update()
    {
        accelerationJob = new AccelerationJob
        {
            deltaTime = Time.deltaTime,
            velocity = velocities,
            acceleration = acceleration,
            accelerationMod = accelerationMod
        };

        job = new PositionUpdateJob
        {
            deltaTime = Time.deltaTime,
            velocity = velocities
        };

        accelJobHandle = accelerationJob.Schedule(particleCount, 64);
        positionJobHandle = job.Schedule(transformAccessArray, accelJobHandle);
    }

    private void LateUpdate()
    {
        positionJobHandle.Complete();
    }

    private void OnDestroy()
    {
        velocities.Dispose();
        transformAccessArray.Dispose();
    }

    private static GameObject[] PlaceRandomSpheres(int count, float radius)
    {
        var cubes = new GameObject[count];
        var objectToCopy = MakeStrippedSphere();

        for (var i = 0; i < count; i++)
        {
            var cube = GameObject.Instantiate(objectToCopy);
            cube.transform.position = Random.insideUnitSphere * radius;
            cubes[i] = cube;
        }

        GameObject.Destroy(objectToCopy);

        return cubes;
    }

    private static GameObject MakeStrippedSphere()
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        // Turn off shadows entirely
        var renderer = sphere.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // Disable collision
        var collider = sphere.GetComponent<Collider>();
        collider.enabled = false;

        return sphere;
    }
}
