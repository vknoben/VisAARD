/// <summary>
/// This script manages the logic behind authoring of 3D instructions
/// </summary>
using MixedReality.Toolkit.SpatialManipulation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MixedReality.Toolkit.UX;
using Serializable3dInstructions;
using SerializableInstructions;
using MixedReality.Toolkit;
using System.Linq;


#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
#endif
namespace Serializable3dInstructions
{
    [Serializable, Tooltip("Used in case of manual authoring")]
    public class Instruction3d
    {
        public string type;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public string color;
    }

    [Serializable]
    public class Instruction3dList
    {
        public List<Instruction3d> instructions;
    }
}

public class Author3d : MonoBehaviour
{
    public static Author3d instance;

    #region Properties

    [Tooltip("Prefab for straight arrow instruction")]
    public GameObject straightArrowPrefab;
    [Tooltip("Prefab for curved arrow instruction")]
    public GameObject curvedArrowPrefab;
    [Tooltip("Prefab for circular arrow instruction")]
    public GameObject circularArrowPrefab;
    [Tooltip("Prefab for hammer instruction")]
    public GameObject hammerPrefab;
    [Tooltip("Prefab for pliers instruction")]
    public GameObject pliersPrefab;
    [Tooltip("Prefab for allen key instruction")]
    public GameObject allenPrefab;
    [Tooltip("Prefab for wrench instruction")]
    public GameObject wrenchPrefab;
    [Tooltip("Prefab for screwdriver instruction")]
    public GameObject screwdriverPrefab;
    [Tooltip("Prefab for open hand instruction")]
    public GameObject openHandPrefab;
    [Tooltip("Prefab for pointing hand instruction")]
    public GameObject pointHandPrefab;
    [Tooltip("Prefab for picking hand instruction")]
    public GameObject pickHandPrefab;


    [Tooltip("Offset for straight arrow to 3d authoring panel")]
    public Vector3 saOffset;
    [Tooltip("Offset for curved arrow to 3d authoring panel")]
    public Vector3 caOffset;
    [Tooltip("Offset for circular arrow to 3d authoring panel")]
    public Vector3 circOffset;
    [Tooltip("Offset for hammer to 3d authoring panel")]
    public Vector3 hammerOffset;
    [Tooltip("Offset for pliers to 3d authoring panel")]
    public Vector3 pliersOffset;
    [Tooltip("Offset for allen key to 3d authoring panel")]
    public Vector3 allenOffset;
    [Tooltip("Offset for wrench to 3d authoring panel")]
    public Vector3 wrenchOffset;
    [Tooltip("Offset for screwdriver to 3d authoring panel")]
    public Vector3 screwdriverOffset;
    [Tooltip("Offset for open hand to 3d authoring panel")]
    public Vector3 openHandOffset;
    [Tooltip("Offset for pointing hand to 3d authoring panel")]
    public Vector3 pointingHandOffset;
    [Tooltip("Offset for picking hand to 3d authoring panel")]
    public Vector3 pickingHandOffset;   

    [Tooltip("Initial scale of straight arrow")]
    public Vector3 saScale;
    [Tooltip("Initial scale of curved arrow")]
    public Vector3 caScale;
    [Tooltip("Initial scale of circular arrow")]
    public Vector3 circScale;
    [Tooltip("Initial scale of hammer")]
    public Vector3 hammerScale;
    [Tooltip("Initial scale of pliers")]
    public Vector3 pliersScale;
    [Tooltip("Initial scale of allen key")]
    public Vector3 allenScale;
    [Tooltip("Initial scale of wrench")]
    public Vector3 wrenchScale;
    [Tooltip("Initial scale of screwdriver")]
    public Vector3 screwdriverScale;
    [Tooltip("Initial scale of hands")]
    public Vector3 handsScale;

    [Tooltip("List containing references to authored objects. Used for deletion on finish")]
    public List<GameObject> authoredObjs;
    [Tooltip("Flag indicating whether currently arranging 3D instruction (authoring in progress")]
    private bool authInProgress = false;
    [Tooltip("Currently authored 3D instruction object")]
    private GameObject authObj;
    [Tooltip("Type of currently authored 3D instruction as string")]
    private string instructionType;

    [Tooltip("List of manually authored 3D instructions. Used for saving and reload")]
    public List<Instruction3d> authoredInstructions;

    public Material whiteMat;
    public Material grayMat;
    public Material brownMat;
    public Material pinkMat;
    public Material redMat;
    public Material purpleMat;
    public Material orangeMat;
    public Material yellowMat;
    public Material limeMat;
    public Material greenMat;
    public Material cyanMat;
    public Material blueMat;
    [Tooltip("Currently selected color as string")]
    private string selectedColor = "white";

    [Tooltip("Flag indicating whether at least one 3D instruction has been authored")]
    public bool _3dAuthored = false;

    [Tooltip("Name/Index of object to be deleted")]
    public int selectedObjIndex;
    //[Tooltip("Indices list of objects that were deleted")]
    //public List<int> deletedObjsIndices;

    #endregion


    #region UI elements

    [Tooltip("3D authoring panel (manual)")]
    public GameObject auth3dPanelManual;
    [Tooltip("Handmenu object")]
    public GameObject handmenu;
    [Tooltip("Button to finalize placement of 3D insturction (handmenu)")]
    public GameObject finalize3dButttonHandmenu;
    [Tooltip("Button to finalize placement of 3D instruction (panel)")]
    public GameObject finalize3dButtonPanel;
    [Tooltip("Button to update previously authored 3D instruction after manipulation")]
    public GameObject update3dButtonPanel;
    [Tooltip("Finish 3D authoring and move on button")]
    public GameObject doneButton;

    [Tooltip("Parent object of authorable 3D instruction options on panel")]
    public GameObject options3dContainer;
    [Tooltip("Parent object of possible color options on panel")]
    public GameObject optionsColorContainer;

    [Tooltip("Delete 3D instruction UI panel")]
    public GameObject deleteObjUI;

    #endregion


    #region UI callbacks

    // Initialize instruction data containers
    public void Initialize3dAuthoring()
    {
        if (authoredObjs == null)
        {
            authoredObjs = new();
            authoredInstructions = new();
        }
        else
        {
            // Make all previously authored 3D objects selectable again
            foreach (var obj in authoredObjs)
            {
                var interactableComps = obj.GetComponents<StatefulInteractable>();
                foreach (var comp in interactableComps)
                {
                    if (comp.GetType() == typeof(ObjectManipulator))
                    {
                        comp.enabled = false;
                    }
                    else if (comp.GetType() == typeof(StatefulInteractable))
                    {
                        comp.enabled = false;
                    }
                }
                foreach (var comp in interactableComps)
                {
                    if (comp.GetType() == typeof(ObjectManipulator))
                    {
                        comp.enabled = false;
                    }
                    else if (comp.GetType() == typeof(StatefulInteractable))
                    {
                        comp.enabled = true;
                    }
                }
            }
        }
    }

    // User selected 3d instruction object
    public void Selected3dObj(string type)
    {
        // Update UI
        doneButton.GetComponent<PressableButton>().enabled = false;

        // Make all previously authored 3D non-manipulable at all for the duration of adding new 3D instruction
        foreach (var prevObjs in authoredObjs)
        {
            var interactableComps = prevObjs.GetComponents<StatefulInteractable>();
            foreach (var comp in interactableComps)
            {
                if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = false;
                }
                else if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = false;
                }
            }
        }

        // Remove previous selection if not manipulated yet
        if (!authInProgress && authObj != null)
        {
            Destroy(authObj);
            authObj = null;
        }

        // Instantiate copy of selected obj
        GameObject obj;
        if (type == "straightArrow")
        {
            obj = Instantiate(straightArrowPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = saOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = saScale;
        }
        else if (type == "curvedArrow")
        {
            obj = Instantiate(curvedArrowPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = caOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = caScale;
        }
        else if (type == "circularArrow")
        {
            obj = Instantiate(circularArrowPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = circOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = circScale;
        }
        else if (type == "hammer")
        {
            obj = Instantiate(hammerPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = hammerOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = hammerScale;
        }
        else if (type == "pliers")
        {
            obj = Instantiate(pliersPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = pliersOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = pliersScale;
        }
        else if (type == "allen")
        {
            obj = Instantiate(allenPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = allenOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = allenScale;
        }
        else if (type == "wrench")
        {
            obj = Instantiate(wrenchPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = wrenchOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = wrenchScale;
        }
        else if (type == "screwdriver")
        {
            obj = Instantiate(screwdriverPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = screwdriverOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = screwdriverScale;
        }
        else if (type == "openHand")
        {
            obj = Instantiate(openHandPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = openHandOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = handsScale;
        }
        else if (type == "pointingHand")
        {
            obj = Instantiate(pointHandPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = pointingHandOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = handsScale;
        }
        else if (type == "pickingHand")
        {
            obj = Instantiate(pickHandPrefab);

            // Place next to 3d authoring panel
            obj.transform.SetParent(auth3dPanelManual.transform, false);
            obj.transform.localPosition = pickingHandOffset;
            // Reparent to world to be independent of panel
            obj.transform.SetParent(null);
            obj.transform.localScale = handsScale;
        }
        else
        {
            return;
        }

        //Log.Msg("Instantiate 3D instruction of type: " + type);

        // Always white as initial color
        if (obj.CompareTag("hand"))
        {
            // In case of hand 3D object
            obj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], whiteMat, obj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
        }
        else
        {
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.material = whiteMat;
            }
        }
        //Log.Msg("Set initial color to white");

        // Disable object options, enable color options
        options3dContainer.SetActive(false);
        optionsColorContainer.SetActive(true);

        // Add listener to manipulation component
        ObjectManipulator objManipulator = obj.GetComponent<ObjectManipulator>();
        objManipulator.selectEntered.AddListener(UserStartedSpatialArrangement);
        objManipulator.enabled = true;

        // Cache as currently authored 3D instruction element
        authObj = obj;
        instructionType = type;
    }

    // User selected color for 3D instruction
    public void SelectedColor(string type)
    {
        // Set selected color as currently selected color
        selectedColor = type;

        if (type == "white")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], whiteMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = whiteMat;
                }
            }            
        }
        else if (type == "gray")
        {     
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], grayMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = grayMat;
                }
            }
        }
        else if (type == "brown")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], brownMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = brownMat;
                }
            }
        }
        else if (type == "pink")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], pinkMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = pinkMat;
                }
            }
        }
        else if (type == "red")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], redMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = redMat;
                }
            }
        }
        else if (type == "purple")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], purpleMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = purpleMat;
                }
            }
        }
        else if (type == "orange")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], orangeMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = orangeMat;
                }
            }
        }
        else if (type == "yellow")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], yellowMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = yellowMat;
                }
            }
        }
        else if (type == "lime")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], limeMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = limeMat;
                }
            }
        }
        else if (type == "green")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], greenMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = greenMat;
                }
            }
        }
        else if (type == "cyan")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], cyanMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = cyanMat;
                }
            }
        }
        else if (type == "blue")
        {
            if (authObj.CompareTag("hand"))
            {
                // In case of hand 3D object
                authObj.GetComponentInChildren<SkinnedMeshRenderer>().SetMaterials(new List<Material>() { authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[0], blueMat, authObj.GetComponentInChildren<SkinnedMeshRenderer>().materials[2] });
            }
            else
            {
                // Regular 3D object
                foreach (var renderer in authObj.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.material = blueMat;
                }
            }
        }
        else
        {
            //Log.Msg("Could not apply color. Color code unknown");
            return;
        }

        //Log.Msg($"Applied color of type: {type}");
    }

    // Called on every manipulation start of a 3D instruction
    public void UserStartedSpatialArrangement(SelectEnterEventArgs eventArgs)
    {
        // Check if this is initial manipulation or not
        if (authInProgress)
        {
            // Not initial manipulation
            return;
        }
        else
        {
            // Initial manipulation -> Disable 3D authoring panel
            //auth3dPanelManual.SetActive(false);

            // Flag as ongoing authoring process
            authInProgress = true;

            // Update controls
            handmenu.SetActive(true);
            finalize3dButttonHandmenu.SetActive(true);
            finalize3dButtonPanel.GetComponent<PressableButton>().enabled = true;
            doneButton.GetComponent<PressableButton>().enabled = false;

            //Log.Msg("Started spatial arrangement of 3D instruction");
        }
    }

    // User finalized 3D instruction
    public void Finalize3dInstruction()
    {
        // Save type, position, rotation, scale relative to qr-code
        Transform qrTransform = QRAuth.instance.qrTransform;
        Instruction3d instruction3d = new Instruction3d
        {
            type = instructionType,
            position = qrTransform.InverseTransformPoint(authObj.transform.position),
            rotation = Quaternion.Inverse(qrTransform.rotation) * authObj.transform.rotation,
            scale = authObj.transform.localScale,
            color = selectedColor
        };
        authoredInstructions.Add(instruction3d);

        // Store reference to Gameobject for latter batch disabling
        authoredObjs.Add(authObj);

        // Set name to number of authored 3D objects for specific reference (and deletion)
        authObj.name = (authoredObjs.Count - 1).ToString();

        // Pass reference to author3d instance for latter deletion
        authObj.GetComponent<DeletableSelectable>().author3d = instance;

        // Not authoring anymore
        authInProgress = false;
        authObj = null;

        // Update UI
        finalize3dButttonHandmenu.SetActive(false);
        finalize3dButtonPanel.GetComponent<PressableButton>().enabled = false;
        doneButton.GetComponent<PressableButton>().enabled = true;

        // Show object options instead of color options again
        options3dContainer.SetActive(true);
        optionsColorContainer.SetActive(false);

        // Flag as 3D authored (at least one object)
        _3dAuthored = true;

        // Make all authored 3D instructions selectable now
        foreach (var prevObjs in authoredObjs)
        {
            var interactableComps = prevObjs.GetComponents<StatefulInteractable>();
            foreach (var comp in interactableComps)
            {
                if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = false;
                }
                else if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = true;
                }
            }
        }
    }

    // Update 3D location of instruction on re-manipulation
    public void Update3dInstruction()
    {
        // Make currently and all other authored 3D objects non-manipulatable, only selectable (strictly make sure not both comps are active at the same time)
        foreach (var obj in authoredObjs)
        {
            var interactableComps = obj.GetComponents<StatefulInteractable>();
            foreach (var comp in interactableComps)
            {
                if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = false;
                }
                else if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = false;
                }
            }
            foreach (var comp in interactableComps)
            {
                if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = false;
                }
                else if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = true;
                }
            }
        }

        // Update instruction information
        Transform qrTransform = QRAuth.instance.qrTransform;
        authoredInstructions[selectedObjIndex].position = qrTransform.InverseTransformPoint(authoredObjs[selectedObjIndex].transform.position);
        authoredInstructions[selectedObjIndex].rotation = Quaternion.Inverse(qrTransform.rotation) * authoredObjs[selectedObjIndex].transform.rotation;
        authoredInstructions[selectedObjIndex].scale = authoredObjs[selectedObjIndex].transform.localScale;

        // Update UI
        update3dButtonPanel.SetActive(false);
        finalize3dButtonPanel.SetActive(true);
        finalize3dButttonHandmenu.SetActive(true);
        finalize3dButtonPanel.GetComponent<PressableButton>().enabled = false;
        doneButton.GetComponent<PressableButton>().enabled = true;

        // Make sure to enable either object or color palette in any case
        foreach (var button in options3dContainer.GetComponentsInChildren<PressableButton>())
        {
            button.enabled = true;
        }
        foreach (var button in optionsColorContainer.GetComponentsInChildren<PressableButton>())
        {
            button.enabled = true;
        }

        // Flag as 3D authored (at least one object)
        _3dAuthored = true;
        authInProgress = false;
    }

    // User finished authoring 3D instructions
    public void Prepare3dForPreview()
    {
        // Remove any preliminary 3D objects 
        if (authObj != null && !authInProgress)
        {
            Destroy(authObj);
            authObj = null;
        }
    }

    // User deletes previously authored 3D instruction
    public void Delete3dInstruction()
    {
        // Remove existing references and destroy object
        authoredInstructions.RemoveAt(selectedObjIndex);
        Destroy(authoredObjs[selectedObjIndex]);
        authoredObjs.RemoveAt(selectedObjIndex);
        //deletedObjsIndices.Add(objToDeleteIndex);

        // Update names/indices of objects since removal causes shift in indices if somewhere inbetween
        for (int i = 0; i < authoredObjs.Count; i++)
        {
            authoredObjs[i].name = i.ToString();
        }

        // Check if any 3D instructions active (if not: prevent user from continuing)
        if (authoredObjs.Count(obj => obj != null && obj.activeInHierarchy) == 0)
        {
            // Prevent user from exiting 3D authoring
            doneButton.GetComponent<PressableButton>().enabled = false;
        }
    }

    // Make selected 3D object manipulable again
    public void Make3dManipulable()
    {
        // Make sure to disable either object or color palette in any case
        foreach (var button in options3dContainer.GetComponentsInChildren<PressableButton>())
        {
            button.enabled = false;
        }
        foreach (var button in optionsColorContainer.GetComponentsInChildren<PressableButton>())
        {
            button.enabled = false;
        }

        // Disable selection of any other 3D objects
        foreach (var prevObjs in authoredObjs)
        {
            var comps = prevObjs.GetComponents<StatefulInteractable>();
            foreach (var comp in comps)
            {
                if (comp.GetType() == typeof(ObjectManipulator))
                {
                    comp.enabled = false;
                }
                else if (comp.GetType() == typeof(StatefulInteractable))
                {
                    comp.enabled = false;
                }
            }
        }

        // Make selected 3D object manipulable (need to strictly not have both of them active at any point in time)
        var interactableComps = authoredObjs[selectedObjIndex].GetComponents<StatefulInteractable>();
        foreach (var comp in interactableComps)
        {
            if (comp.GetType() == typeof(ObjectManipulator))
            {
                comp.enabled = true;
            }
            else if (comp.GetType() == typeof(StatefulInteractable))
            {
                comp.enabled = false;
            }
        }

        // Update UI
        update3dButtonPanel.SetActive(true);
        finalize3dButtonPanel.SetActive(false);
        finalize3dButttonHandmenu.SetActive(false);
        doneButton.GetComponent<PressableButton>().enabled = false;
    }

    #endregion


    #region Methods

    // Save 3D instructions to disk
    public async Task Save3dInstructions()
    {
        // Remove any previously deleted objects before saving. Sort and reverse for memory-save removal
        //deletedObjsIndices.Sort();
        //deletedObjsIndices.Reverse();
        //foreach (int i in deletedObjsIndices)
        //{
        //    authoredObjs.RemoveAt(i);
        //    authoredInstructions.RemoveAt(i);
        //}

        // Serialize 3d instructions based on unified format (same as semi-automatically authored instructions)
        SerializableInsituInstructionSet serializableSet = new SerializableInsituInstructionSet(authoredInstructions);
        string json = JsonUtility.ToJson(serializableSet, true);

#if ENABLE_WINMD_SUPPORT
        // Create .json file containing 3D instruction information
        string fileName = "3d.json";
        StorageFile file = await WorkflowManager.instance.currentStepFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(file, json);

        //Log.Msg("Wrote 3D instruction data to file");
#endif
    }

    // Cleans up all manually authored in-situ 3d instructions
    public void Reset3dAuth()
    {
        // Remove 3D instructions visibly or disable
        foreach (var obj in authoredObjs)
        {
            //Destroy(obj);

            obj.SetActive(false);
        }

        // Clear data
        authoredInstructions = new List<Instruction3d>();
        authoredObjs = new List<GameObject>();
        authInProgress = false;
        authObj = null;
        instructionType = string.Empty;
        _3dAuthored = false;
    }

    #endregion


    #region Unity lifecycle

    // Awake is called even if object not active
    void Awake()
    {
        // Make sure only one instance of this class exists
        if (instance != null && instance != this)
        {
            Destroy(this);
        }
        else
        {
            instance = this;
        }
    }

    #endregion
}