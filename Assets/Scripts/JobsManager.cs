using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

public class JobsManager : MonoBehaviour
{

    // GUI
    [Header("GUI")]
    [SerializeField]
    Text m_TextFps;

    [SerializeField]
    InputField m_InputField;

    [SerializeField]
    Button m_ButtonSpawn;

    [SerializeField]
    Button m_ButtonKick;

    [SerializeField]
    Button m_ButtonStart;


    [SerializeField]
    Text m_BatchSizeTest;

    [SerializeField]
    Dropdown m_DropDown;

    [SerializeField]
    Button m_ButtonBatchMinus;
    [SerializeField]
    Button m_ButtonBatchPlus1;
    [SerializeField]
    Button m_ButtonBatchPlus10;
    [SerializeField]
    Button m_ButtonBatchPlus100;


    // number of spawned objects
    private static int maxArray;

    [Header("MISC")]
    [SerializeField]
    bool enableProcess = false;

    [SerializeField]
    bool enableCalculation = false;

    [SerializeField]
    private float forceMultiplier;

    private Vector3 forceOnCenter;

    public static readonly float G = 0.00000000006675f;

    [SerializeField]
    public static int j_BatchCount = 0;

    private List<float> m_ListAverageTime = new List<float>();
    private float m_averateTime = 0;



    [Header("Prefab")]
    [SerializeField]
    private GameObject _prefabMoon;

    // moon objects
    private GameObject[] moonGameobjects;
    private Vector3[] moonVectors;
    private float[] moonMasses;
    private Vector3[] moonForces;
    private Rigidbody[] moonRigidbodies;



    /******************************/
    /**********  JOBS  ************/
    /******************************/

    // moon objects
    NativeArray<Vector3> m_MoonVectors;
    NativeArray<float> m_MoonMasses;
    NativeArray<Vector3> m_MoonForces;

    // Jobs
    Gravity_IJob m_GravityIJob;
    Gravity_IJobFor m_GravityIJobFor;
    Gravity_IJobParallerFor m_GravityIJobParallerFor;

    public enum EnumOption { MainThreat, IJob, IJobFor, IJobParallerFor };

    public EnumOption currentOption;

    // Start is called before the first frame update
    void Start()
    {
        // MISC
        enableCalculation = false;
        enableProcess = false;
        forceMultiplier = 10000000;
        j_BatchCount = 0;
        m_ListAverageTime.Clear();
        EnableBatch(false);

        // GUI
        m_InputField.text = "3";
        m_BatchSizeTest.text = "Batch count: " + j_BatchCount.ToString();
        m_ButtonStart.interactable = true;
        m_ButtonKick.interactable = false;

    }

    // Update is called once per frame
    void Update()
    {
        if (!enableCalculation)
            return;

        float startTime = Time.realtimeSinceStartup;

        // ######################
        // START COMPUTATION HERE
        // ######################

        UpdateMoonVectors();

        switch (currentOption)
        {
            case EnumOption.MainThreat:
                MainThreatCalculations();
                break;

            case EnumOption.IJob:
                IJobCalculations();
                break;

            case EnumOption.IJobFor:
                IJobForCalculations();
                break;

            case EnumOption.IJobParallerFor:
                IJobParallelForCalculations();
                break;
        }

        // ####################
        // END COMPUTATION HERE
        // ####################

        float duration = (Time.realtimeSinceStartup - startTime) * 1000f;

        m_ListAverageTime.Add(duration);
        if (m_ListAverageTime.Count > 10)  //Remove the oldest when we have more than 10
        {
            m_ListAverageTime.RemoveAt(0);
        }

        float averageDuration = 0f;
        foreach (float f in m_ListAverageTime)  //Calculate the total of all floats
        {
            averageDuration += f;
        }
        float averateTime = averageDuration / (float)m_ListAverageTime.Count;
        m_TextFps.text = duration.ToString("F2") + " ms / " + averateTime.ToString("F2") + " ms";
    }

    private void GenerateMoons()
    {
        moonVectors = new Vector3[maxArray];
        moonMasses = new float[maxArray];
        moonForces = new Vector3[maxArray];
        moonGameobjects = new GameObject[maxArray];
        moonRigidbodies = new Rigidbody[maxArray];

        for (int i = 0; i < maxArray; i++)
        {
            float randomScale = UnityEngine.Random.Range(2, 11);    // random scale size
            Vector3 spawnPosition = new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100));

            moonGameobjects[i] = Instantiate(_prefabMoon.gameObject);
            moonGameobjects[i].transform.position = spawnPosition;
            moonGameobjects[i].transform.SetParent(transform);
            moonGameobjects[i].transform.localScale = new Vector3(randomScale, randomScale, randomScale);

            moonVectors[i] = moonGameobjects[i].transform.position;
            moonMasses[i] = UnityEngine.Random.Range(1, 10000); // random mass value

            moonRigidbodies[i] = moonGameobjects[i].GetComponent<Rigidbody>();
            moonRigidbodies[i].mass = moonMasses[i];

            moonForces[i] = Vector3.zero;
        }

        m_MoonVectors = new NativeArray<Vector3>(moonVectors, Allocator.Persistent);
        m_MoonMasses = new NativeArray<float>(moonMasses, Allocator.Persistent);
        m_MoonForces = new NativeArray<Vector3>(moonForces, Allocator.Persistent);

    }

    private void UpdateMoonVectors()
    {
        for (int i = 0; i < maxArray; i++)
        {
            moonVectors[i] = moonGameobjects[i].transform.position;

            m_MoonVectors.Dispose();
            m_MoonVectors = new NativeArray<Vector3>(moonVectors, Allocator.Persistent);
        }
    }

    private void DestroyMoons()
    {
        foreach (GameObject go in moonGameobjects)
        {
            Destroy(go);
        }
    }

    private void OnDestroy()
    {
        DestroyMoonsJobs();
        DestroyMoons();
    }


    // ########################
    // MAIN THREAT CALCULATIONS
    // ########################

    private void MainThreatCalculations()
    {
        for (int i = 0; i < maxArray; i++)
        {
            Vector3 force = CalculateForceVector(moonVectors[i], moonMasses[i]);
            moonRigidbodies[i].AddForce(force * Time.deltaTime * forceMultiplier);
        }
    }

    private Vector3 CalculateForceVector(Vector3 planetVector, float planetMass)
    {
        forceOnCenter = Vector3.zero;

        for (int i = 0; i < maxArray; i++)
        {
            Vector3 v3Distance = planetVector - moonVectors[i];

            if (v3Distance == Vector3.zero)
            {
                continue;
            }

            float distance = Mathf.Sqrt(v3Distance.sqrMagnitude);

            float forceForce = G * ((moonMasses[i] * planetMass) / distance);

            Vector3 dir = (moonVectors[i] - planetVector).normalized;

            moonForces[i] = (dir * forceForce);
        }

        foreach (Vector3 moonForce in moonForces)
        {
            forceOnCenter += moonForce;
        }

        Debug.DrawLine(planetVector, planetVector + forceOnCenter * 10000, Color.white, 0.25f);

        return forceOnCenter;
    }

    // ########################
    // ##### JOBS
    // ########################

    #region IJob

    private void IJobCalculations()
    {
        for (int i = 0; i < maxArray; i++)
        {
            Vector3 force = Calculate_IJob(moonVectors[i], moonMasses[i]);
            moonRigidbodies[i].AddForce(force * Time.deltaTime * forceMultiplier);
        }
    }

    public Vector3 Calculate_IJob(Vector3 planetVector, float planetMass)
    {
        forceOnCenter = Vector3.zero;

        m_GravityIJob = new Gravity_IJob()
        {
            j_PlanetVector = planetVector,
            j_PlanetMass = planetMass,
            j_MoonVectors = m_MoonVectors,
            j_MoonMasses = m_MoonMasses,
            j_MoonForces = m_MoonForces
        };

        JobHandle m_JobHandleJob = m_GravityIJob.Schedule();
        m_JobHandleJob.Complete();
        m_GravityIJob.j_MoonForces.CopyTo(m_MoonForces);

        foreach (Vector3 moonForce in m_MoonForces)
        {
            forceOnCenter += moonForce;
        }

        Debug.DrawLine(planetVector, planetVector + forceOnCenter * 10000, Color.white, 0.25f);

        return forceOnCenter;

    }

    [BurstCompile]
    struct Gravity_IJob : IJob
    {
        public Vector3 j_PlanetVector;
        public float j_PlanetMass;

        public NativeArray<Vector3> j_MoonVectors;
        public NativeArray<float> j_MoonMasses;
        public NativeArray<Vector3> j_MoonForces;

        public void Execute()
        {
            for (var i = 0; i < j_MoonVectors.Length; i++)
            {
                var v3Distance = j_PlanetVector - j_MoonVectors[i];

                if (v3Distance != Vector3.zero)
                {
                    var distance = Mathf.Sqrt(v3Distance.sqrMagnitude);

                    var forceForce = 0.00000000006675f * ((j_MoonMasses[i] * j_PlanetMass) / distance);

                    var dir = (j_MoonVectors[i] - j_PlanetVector).normalized;

                    j_MoonForces[i] = (dir * forceForce);
                }
            }
        }
    }

    #endregion

    #region IJobFor

    private void IJobForCalculations()
    {
        for (int i = 0; i < maxArray; i++)
        {
            Vector3 force = Calculate_IJobFor(moonVectors[i], moonMasses[i]);
            moonRigidbodies[i].AddForce(force * Time.deltaTime * forceMultiplier);
        }
    }

    public Vector3 Calculate_IJobFor(Vector3 planetVector, float planetMass)
    {
        forceOnCenter = Vector3.zero;

        m_GravityIJobFor = new Gravity_IJobFor()
        {
            j_PlanetVector = planetVector,
            j_PlanetMass = planetMass,
            j_MoonVectors = m_MoonVectors,
            j_MoonMasses = m_MoonMasses,
            j_MoonForces = m_MoonForces
        };

        JobHandle sheduleJobDependency = new JobHandle();
        JobHandle m_JobHandleJobFor = m_GravityIJobFor.Schedule(maxArray, sheduleJobDependency);
        m_JobHandleJobFor.Complete();
        m_GravityIJobFor.j_MoonForces.CopyTo(m_MoonForces);

        foreach (Vector3 moonForce in m_MoonForces)
        {
            forceOnCenter += moonForce;
        }

        Debug.DrawLine(planetVector, planetVector + forceOnCenter * 10000, Color.white, 0.25f);

        return forceOnCenter;

    }

    [BurstCompile]
    struct Gravity_IJobFor : IJobFor
    {
        public Vector3 j_PlanetVector;
        public float j_PlanetMass;

        public NativeArray<Vector3> j_MoonVectors;
        public NativeArray<float> j_MoonMasses;
        public NativeArray<Vector3> j_MoonForces;


        public void Execute(int i)
        {
            var v3Distance = j_PlanetVector - j_MoonVectors[i];

            if (v3Distance != Vector3.zero)
            {
                var distance = Mathf.Sqrt(v3Distance.sqrMagnitude);

                var forceForce = 0.00000000006675f * ((j_MoonMasses[i] * j_PlanetMass) / distance);

                var dir = (j_MoonVectors[i] - j_PlanetVector).normalized;

                j_MoonForces[i] = (dir * forceForce);
            }
        }
    }

    #endregion

    #region IJobParallerFor

    private void IJobParallelForCalculations()
    {
        for (int i = 0; i < maxArray; i++)
        {
            Vector3 force = Calculate_IJobParallerFor(moonVectors[i], moonMasses[i]);
            moonRigidbodies[i].AddForce(force * Time.deltaTime * forceMultiplier);
        }
    }

    public Vector3 Calculate_IJobParallerFor(Vector3 planetVector, float planetMass)
    {
        forceOnCenter = Vector3.zero;

        m_GravityIJobParallerFor = new Gravity_IJobParallerFor()
        {
            j_PlanetVector = planetVector,
            j_PlanetMass = planetMass,
            j_MoonVectors = m_MoonVectors,
            j_MoonMasses = m_MoonMasses,
            j_MoonForces = m_MoonForces
        };

        JobHandle m_JobHandleJobParallerFor = m_GravityIJobParallerFor.Schedule(maxArray, j_BatchCount);
        m_JobHandleJobParallerFor.Complete();
        m_GravityIJobParallerFor.j_MoonForces.CopyTo(m_MoonForces);

        foreach (Vector3 moonForce in m_MoonForces)
        {
            forceOnCenter += moonForce;
        }

        Debug.DrawLine(planetVector, planetVector + forceOnCenter * 10000, Color.white, 0.25f);

        return forceOnCenter;

    }

    [BurstCompile]
    struct Gravity_IJobParallerFor : IJobParallelFor
    {
        public Vector3 j_PlanetVector;
        public float j_PlanetMass;

        public NativeArray<Vector3> j_MoonVectors;
        public NativeArray<float> j_MoonMasses;
        public NativeArray<Vector3> j_MoonForces;


        public void Execute(int i)
        {
            var v3Distance = j_PlanetVector - j_MoonVectors[i];

            if (v3Distance != Vector3.zero)
            {
                var distance = Mathf.Sqrt(v3Distance.sqrMagnitude);

                var forceForce = 0.00000000006675f * ((j_MoonMasses[i] * j_PlanetMass) / distance);

                var dir = (j_MoonVectors[i] - j_PlanetVector).normalized;

                j_MoonForces[i] = (dir * forceForce);
            }
        }
    }

    #endregion


    private void DestroyMoonsJobs()
    {
        m_MoonForces.Dispose();
        m_MoonMasses.Dispose();
        m_MoonVectors.Dispose();
    }

    // ########################
    // ##### UI Inputs
    // ########################

    public void InputFieldChange()
    {
        int inputFieldNumber = int.Parse(m_InputField.text);
        int realNumber = Mathf.Clamp(inputFieldNumber, 3, 100000);
        m_InputField.text = realNumber.ToString();
    }

    public void ButtonKickClick()
    {
        for (int i = 0; i < maxArray; i++)
        {
            Vector3 randomKick = new Vector3(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(-10, 10));
            moonRigidbodies[i].AddForce(randomKick * (forceMultiplier / 200000));
        }
        Debug.Log("KickMoons");
    }

    public void ButtonSpawnClick()
    {
        enableProcess = !enableProcess;
        m_ButtonKick.interactable = enableProcess;

        if (enableProcess)
        {
            // get number
            maxArray = int.Parse(m_InputField.text);

            // spawn objects
            GenerateMoons();

            m_ButtonSpawn.GetComponentInChildren<Text>().text = "DESTROY MOONS";
        }
        else
        {
            // Stop calculation
            if (enableCalculation)
                ButtonStartClick();

            // delete objects
            DestroyMoonsJobs();
            DestroyMoons();

            m_ButtonSpawn.GetComponentInChildren<Text>().text = "SPAWN MOONS";

        }
    }

    public void ButtonStartClick()
    {
        enableCalculation = !enableCalculation;
        if (enableCalculation)
            m_ButtonStart.GetComponentInChildren<Text>().text = "STOP CALCULATION";
        else
            m_ButtonStart.GetComponentInChildren<Text>().text = "START CALCULATION";
    }

    public void ButtonBatchSizePlus1()
    {
        UpdateBatchText(1);
    }

    public void ButtonBatchSizePlus10()
    {
        UpdateBatchText(10);
    }

    public void ButtonBatchSizePlus100()
    {
        UpdateBatchText(100);
    }

    public void ButtonBatchSizeMinus()
    {
        UpdateBatchText(-100);
    }

    private void UpdateBatchText(int newBatch)
    {
        j_BatchCount += newBatch;
        if (j_BatchCount < 0)
            j_BatchCount = 0;
        m_BatchSizeTest.text = "Batch count: " + j_BatchCount.ToString();
    }

    public void DropDownOnValueChanged()
    {
        Debug.Log(m_DropDown.value);
        switch (m_DropDown.value)
        {
            case 0:
                currentOption = EnumOption.MainThreat;
                EnableBatch(false);
                break;
            case 1:
                currentOption = EnumOption.IJob;
                EnableBatch(false);
                break;
            case 2:
                currentOption = EnumOption.IJobFor;
                EnableBatch(false);
                break;
            case 3:
                currentOption = EnumOption.IJobParallerFor;
                EnableBatch(true);
                break;
        }

    }

    private void EnableBatch(bool status)
    {
        m_ButtonBatchMinus.interactable = status;
        m_ButtonBatchPlus1.interactable = status;
        m_ButtonBatchPlus10.interactable = status;
        m_ButtonBatchPlus100.interactable = status;

    }

    public void ButtonQuit()
    {
        Application.Quit();
    }

}
