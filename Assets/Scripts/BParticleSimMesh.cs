using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*** a taylor's note (from BrightSpace instructions):
* ground contact penalty spring eq. =
* -ks((particle position - ground plane point)dot.ground plane normal)*ground plane normal - contact penalty spring damping coefficient*particle velocity
* 
* -BContactSpring.ks(BParticle.position - BPlane.position)dot.BPlane.normal - BContactSpring.kd * BParticle.velocity
* 
* eq in code: Vector3 contactForce = -BParticle.contactSpring.ks * Vector3.Dot(BParticle.position - BParticle.contactSpring.attachPoint, BPlane.normal) * BPlane.normal - BParticle.contactSpring.kd * BParticle.velocity;
* 
* cont. BParticle.currentForces += contactForce
* 
***/

/*** a taylor's note (from BrightSpace instructions):
 * particle-particle spring equation ** not same values as variables above
 * 
 * ks((l - |particle1 pos - particle2 pos|) (particle1 pos - particle2 pos) / (|particle1 pos - particle2 pos|) - kd((particle1 velocity - particle2 velocity)Dot((particle1 pos - particle2 pos) / (|particle1 pos - particle2 pos|)) * (particle1 pos - particle2 pos) / (|particle1 pos - particle2 pos|)
 * 
 * BParticle1.BSpring.ks * ((BParticle1.BSpring.restLength - |BParticle1.position - BParticle2.position|) * ((BParticle1.position - BParticle2.position) / (|BParticle1.position - BParticle2.position|))) - (BParticle1.BSpring.kd * (BParticle1.velocity - BParticle2.velocity)Dot.((BParticle1.position - BParticle2.position) / (|BParticle1.position - BParticle2.position|)) * ((BParticle1.position - BParticle2.position) / (|BParticle1.position - BParticle2.position|)))
 * 
 ***/

// Check this out we can require components be on a game object!
[RequireComponent(typeof(MeshFilter))]

public class BParticleSimMesh : MonoBehaviour
{
    public struct BSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring
        public int attachedParticle;            // index of the attached other particle (use me wisely to avoid doubling springs and sprign calculations)
    }

    public struct BContactSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring (think about this ... may not even be needed o_0
        public Vector3 attachPoint;             // the attached point on the contact surface
    }

    public struct BParticle
    {
        public Vector3 position;                // position information
        public Vector3 velocity;                // velocity information
        public float mass;                      // mass information
        public BContactSpring contactSpring;    // Special spring for contact forces
        public bool attachedToContact;          // is this particle currently attached to a contact (ground plane contact)
        public List<BSpring> attachedSprings;   // all attached springs, as a list in case we want to modify later fast
        public Vector3 currentForces;           // accumulate forces here on each step        
    }

    public struct BPlane
    {
        public Vector3 position;                // plane position
        public Vector3 normal;                  // plane normal
    }

    public float contactSpringKS = 1000.0f;     // contact spring coefficient with default 1000
    public float contactSpringKD = 20.0f;       // contact spring damping coefficient with default 20

    public float defaultSpringKS = 100.0f;      // default spring coefficient with default 100
    public float defaultSpringKD = 1.0f;        // default spring damping coefficient with default 1

    public bool debugRender = false;            // To render or not to render

    /*** 
     * I've given you all of the above to get you started
     * Here you need to publicly provide the:
     * - the ground plane transform (Transform)
     * - handlePlaneCollisions flag (bool)
     * - particle mass (float)
     * - useGravity flag (bool)
     * - gravity value (Vector3)
     * 
     * Here you need to privately provide the:
     * - Mesh (Mesh)
     * - array of particles (BParticle[])
     * - the plane (BPlane)
     * 
     ***/

    public Transform groundPlaneTransform;
    public bool handlePlaneCollisions = false; // set default value 
    public float particleMass = 1.0f; // set default value 
    public bool useGravity = false; // set default value
    public Vector3 gravityValue = new Vector3(0, -9.81f, 0); // set default gravity to actual

    private Mesh Mesh;
    private BParticle[] particles;
    private BPlane plane; 

    /// <summary>
    /// Init everything
    /// HINT: in particular you should probabaly handle the mesh, init all the particles, and the ground plane
    /// HINT 2: I'd for organization sake put the init particles and plane stuff in respective functions
    /// HINT 3: Note that mesh vertices when accessed from the mesh filter are in local coordinates.
    ///         This script will be on the object with the mesh filter, so you can use the functions
    ///         transform.TransformPoint and transform.InverseTransformPoint accordingly 
    ///         (you need to operate on world coordinates, and render in local)
    /// HINT 4: the idea here is to make a mathematical particle object for each vertex in the mesh, then connect
    ///         each particle to every other particle. Be careful not to double your springs! There is a simple
    ///         inner loop approach you can do such that you attached exactly one spring to each particle pair
    ///         on initialization. Then when updating you need to remember a particular trick about the spring forces
    ///         generated between particles. 
    /// </summary>
    void Start()
    {

    }

    /*** BIG HINT: My solution code has as least the following functions
     * InitParticles()
     * InitPlane()
     * UpdateMesh() (remember the hint above regarding global and local coords)
     * ResetParticleForces()
     * ...
     ***/

    private void InitParticles()
    {
        Mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = Mesh.vertices;
        int vertexCount = vertices.Length;
        particles = new BParticle[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            // convert local vertices to world coords : transform.TransformPoint(vertices[i]) --- to revert back to local we will use transform.InverseTransformPoint(particle.position)
            particles[i] = new BParticle {position = transform.TransformPoint(vertices[i]), velocity = Vector3.zero, mass = particleMass, attachedSprings = new List<BSpring>(), currentForces = Vector3.zero, attachedToContact = false, contactSpring = new BContactSpring{ks = contactSpringKS, kd = contactSpringKD, attachPoint = Vector3.zero, restLength = 0}};
        }
    }

    private void InitPlane()
    {
        if (groundPlaneTransform != null)
        {
            plane.position = groundPlaneTransform.position; //set plane position based on transform
            plane.normal = groundPlaneTransform.up; //set plane normal based on transform *up* is used, not normal based on unity doc
        }
        else
        {
            Debug.LogWarning("ground plane transform was not set, speaking to you from InitPlane()");
            plane.position = Vector3.zero;
            plane.normal = Vector3.up;
        }
    }

    private void ResetParticleForces()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].currentForces = Vector3.zero;
        }
    }

    private void ComputeAllForces()
    {
        // make sure we start fresh by clearing the forces
        ResetParticleForces();
        int particleCount = particles.Length;

        //if gravity = true then we will use gravity, so we apply that here
        if (useGravity)
        {
            for (int i = 0; i < particleCount; i++)
            {
                BParticle p = particles[i];
                p.currentForces += p.mass * gravityValue;
                particles[i] = p;
            }
        }

        //particle-particle spring forces **recall no mesh-mesh as per instructions
        for (int i = 0; i < particleCount; i++)
        {
            BParticle p1 = particles[i]; // particle1: (we need to pull particle1 from the particles list)
            for (int j = 0; j < p1.attachedSprings.Count; j++)
            {
                BSpring theSpring = p1.attachedSprings[j];
                BParticle p2 = particles[theSpring.attachedParticle]; //particle2 is pulled from what is attached to the p1
                //prepping dir and dist before subbing them into the particle-particle spring eq to simplify it somewhat
                Vector3 theDirection = (p1.position - p2.position).normalized;
                float theDistance = (p1.position - p2.position).magnitude;
                // from taylor's notes/BS, translated to code:
                // * BParticle1.BSpring.ks * ((BParticle1.BSpring.restLength - |BParticle1.position - BParticle2.position|) * ((BParticle1.position - BParticle2.position) / (|BParticle1.position - BParticle2.position|))) - (BParticle1.BSpring.kd * (BParticle1.velocity - BParticle2.velocity)Dot.((BParticle1.position - BParticle2.position) / (|BParticle1.position - BParticle2.position|)) * ((BParticle1.position - BParticle2.position) / (|BParticle1.position - BParticle2.position|)))
                Vector3 springForce = theSpring.ks * (theSpring.restLength - theDistance) * theDirection - theSpring.kd * Vector3.Dot(p1.velocity - p2.velocity, theDirection) * theDirection;
                p1.currentForces += springForce;
            }
            particles[i] = p1;
        }

        //ground contact penetration penalty spring
        if (handlePlaneCollisions)
        {
            for (int i = 0; i < particleCount; i++)
            {
                BParticle p = particles[i];

                //p below or above plane??
                float aboveOrBelow = Vector3.Dot(p.position - plane.position, plane.normal);
                if (aboveOrBelow < 0f) //point is below the plane
                {
                    //ground contact penalty spring eq. from taylor's notes/BS, translated to code:
                    //Vector3 contactForce = -BParticle.contactSpring.ks * Vector3.Dot(BParticle.position - BParticle.contactSpring.attachPoint, BPlane.normal) * BPlane.normal - BParticle.contactSpring.kd * BParticle.velocity;
                    Vector3 contactForce = -p.contactSpring.ks * aboveOrBelow * plane.normal - p.contactSpring.kd * p.velocity;
                    p.currentForces += contactForce;
                    p.attachedToContact = true;
                    p.contactSpring.attachPoint = plane.position;
                }
                else
                {
                    p.attachedToContact = false;
                }
                particles[i] = p;
            }
        }
    }

    private void UpdateMesh()
    {

    }

    /// <summary>
    /// Draw a frame with some helper debug render code
    /// </summary>
    public void Update()
    {
        /* This will work if you have a correctly made particles array
        if (debugRender)
        {
            int particleCount = particles.Length;
            for (int i = 0; i < particleCount; i++)
            {
                Debug.DrawLine(particles[i].position, particles[i].position + particles[i].currentForces, Color.blue);

                int springCount = particles[i].attachedSprings.Count;
                for (int j = 0; j < springCount; j++)
                {
                    Debug.DrawLine(particles[i].position, particles[particles[i].attachedSprings[j].attachedParticle].position, Color.red);
                }
            }
        }
        */
    }
}
