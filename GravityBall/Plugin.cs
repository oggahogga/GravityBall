using BepInEx;
using BepInEx.Configuration;
using System;
using UnityEngine;
using Utilla;

namespace GravityBall
{
    [ModdedGamemode]
    [BepInDependency("org.legoandmars.gorillatag.utilla", "1.5.0")]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        private bool inroom;
        private Transform rightcontrollertransform;
        private bool isgrabbing = false;
        private bool spherespawned = false;
        private GameObject spawnedsphere;
        private Rigidbody sphererb;
        private ConfigEntry<float> spheresize;
        private ConfigEntry<float> pullforce;
        private ConfigEntry<float> maxdistance;
        private ConfigEntry<float> springconstant;
        private ConfigEntry<float> dampingfactor;
        private ConfigEntry<float> controllervelocitymultiplier;

        void Awake()
        {
            try
            {
                spheresize = Config.Bind("General", "spheresize", 0.2f, "size of sphere");
                pullforce = Config.Bind("General", "pullforce", 4.5f, "pull force towards the hand");
                maxdistance = Config.Bind("General", "maxdistance", 1.5f, "max distance from hand");
                springconstant = Config.Bind("General", "springconstant", 20.0f, "elastic amount");
                dampingfactor = Config.Bind("General", "dampingfactor", 4.0f, "spring damp");
                controllervelocitymultiplier = Config.Bind("General", "controllervelocitymultiplier", 6.0f, "how much it mixes the controller velocity with the elastic velocity, 0 for off.");

                Debug.Log("Config file loaded successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading config file: {e}");
            }
        }

        void Start()
        {
            Utilla.Events.GameInitialized += OnGameInitialized;
        }

        void OnEnable()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }

        void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
        }

        void OnGameInitialized(object sender, EventArgs e)
        {
            if (GorillaLocomotion.Player.Instance != null)
            {
                rightcontrollertransform = GorillaLocomotion.Player.Instance.rightControllerTransform;
                Debug.Log("Game Initialized: Right controller transform set.");
            }
        }

        private void spawnsphere()
        {
            if (rightcontrollertransform != null)
            {
                if (!spherespawned)
                {
                    if (spawnedsphere == null)
                    {
                        spawnedsphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        spawnedsphere.GetComponent<Collider>().enabled = false;
                        spawnedsphere.transform.position = rightcontrollertransform.position;
                        spawnedsphere.transform.rotation = Quaternion.identity;
                    }
                    spawnedsphere.GetComponent<Renderer>().material.shader = Shader.Find("GUI/Text Shader");
                    spawnedsphere.GetComponent<Renderer>().material.color = Color.blue - new Color(0, 0, 0, 0.5f);
                    spawnedsphere.transform.localScale = new Vector3(spheresize.Value, spheresize.Value, spheresize.Value);
                    sphererb = spawnedsphere.GetComponent<Rigidbody>();
                    if (sphererb == null)
                    {
                        sphererb = spawnedsphere.AddComponent<Rigidbody>();
                    }
                    sphererb.useGravity = false;
                    spherespawned = true;
                }
            }
        }

        public static void curvledline(LineRenderer linerenderer, Vector3 start, Vector3 middle, Vector3 end)
        {
            int pointCount = 200;
            linerenderer.positionCount = pointCount;
            float amplitude = 0.025f; // Wave height
            float frequency = 7f; // Number of waves
            float speed = 10f; // Wave speed
            Vector3 B = Vector3.zero;

            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                B = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * middle + t * t * end;
                float wave = Mathf.Sin(t * Mathf.PI * frequency + Time.time * speed) * amplitude;
                Vector3 perpendicular = Vector3.Cross(end - start, Vector3.up).normalized;
                B += perpendicular * wave;
                linerenderer.SetPosition(i, B);
            }
        }

        public static Vector3 lerpingmiddle;

        void Update()
        {
            if (inroom)
            {
                bool rightgrab = ControllerInputPoller.instance.rightGrab;

                if (rightgrab)
                {
                    if (!isgrabbing)
                    {
                        isgrabbing = true;
                        spawnsphere();
                    }
                    GameObject lineFollow = new GameObject("Line");
                    LineRenderer lineUser = lineFollow.AddComponent<LineRenderer>();

                    lineUser.startWidth = 0.01f;
                    lineUser.endWidth = 0.01f;
                    lineUser.useWorldSpace = true;
                    lineUser.positionCount = 2;
                    Vector3 middle = (rightcontrollertransform.position + spawnedsphere.transform.position) / 2;
                    lerpingmiddle = Vector3.Lerp(lerpingmiddle, middle, Time.deltaTime * 5);
                    curvledline(lineUser, rightcontrollertransform.position, lerpingmiddle, spawnedsphere.transform.position);
                    lineUser.material.shader = Shader.Find("GUI/Text Shader");
                    lineUser.material.color = spawnedsphere.GetComponent<Renderer>().material.color;
                    lineUser.material.SetFloat("_Glossiness", 0.0f);
                    lineUser.material.EnableKeyword("_EMISSION");
                    lineUser.material.SetColor("_EmissionColor", new Color32(0, 255, 0, 125));
                    lineUser.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    Destroy(lineFollow, Time.deltaTime);
                }
                else
                {
                    if (isgrabbing && spawnedsphere != null)
                    {
                        isgrabbing = false;
                        spherespawned = false;
                    }
                }
            }
            else
            {
                if (spawnedsphere != null)
                {
                    Destroy(spawnedsphere);
                }
            }
        }

        void FixedUpdate()
        {
            if (isgrabbing && spawnedsphere != null)
            {
                movespheretowardshand();
            }
        }

        private void movespheretowardshand()
        {
            if (sphererb != null && rightcontrollertransform != null)
            {
                Vector3 directiontocontroller = rightcontrollertransform.position - sphererb.position;
                float distancetocontroller = directiontocontroller.magnitude;

                Vector3 pullforce = directiontocontroller.normalized * this.pullforce.Value;
                sphererb.AddForce(pullforce, ForceMode.Acceleration);

                Vector3 controllervelocity = OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch);
                sphererb.AddForce(controllervelocity * controllervelocitymultiplier.Value, ForceMode.Acceleration);
                if (distancetocontroller > maxdistance.Value)
                {
                    Vector3 springforce = springconstant.Value * directiontocontroller;
                    sphererb.AddForce(springforce, ForceMode.Acceleration);

                    Vector3 dampingforce = -dampingfactor.Value * sphererb.velocity;
                    sphererb.AddForce(dampingforce, ForceMode.Acceleration);
                }
            }
        }

        [ModdedGamemodeJoin]
        public void OnJoin(string gamemode)
        {
            inroom = true;
            Debug.Log($"Joined game mode: {gamemode}");
        }

        [ModdedGamemodeLeave]
        public void OnLeave(string gamemode)
        {
            inroom = false;
            Debug.Log($"Left game mode: {gamemode}");
        }
    }
}
