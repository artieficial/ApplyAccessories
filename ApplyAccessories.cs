using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif
using UnityEngine;
using UnityEngine.Animations;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

[CustomEditor(typeof(ApplyAccessories))]
public class ApplyAccessoriesEditor : Editor
{
    public Transform targetArmature;
    public VRCAvatarDescriptor targetAvatarDescriptor;
    public AnimationClip targetFemaleAnimationClip;
    public bool targetNestArmature;
    public Transform targetRootBone;
    public VRCExpressionsMenu targetSubMenu;
    public SkinnedMeshRenderer[] associatedMeshRenderers;
    public Dictionary<SkinnedMeshRenderer, string[]> associatedBlendShapesMap;
    public string femaleBlendShapeName = "";
    bool showOverrideArmature = true;
    bool showFemaleBlendShapes = true;
    bool showAssociatedMeshRenderers = true;
    bool[] showPanelsForAssociatedMeshRenderers = {};

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        ApplyAccessories applyAccessories = (ApplyAccessories)target;

        // Make sure that all the values are loaded from the actual component
        targetArmature = applyAccessories.getTargetArmature();
        targetAvatarDescriptor = applyAccessories.getTargetAvatarDescriptor();
        targetFemaleAnimationClip = applyAccessories.getTargetFemaleAnimationClip();
        targetNestArmature = applyAccessories.getTargetNestArmature();
        targetRootBone = applyAccessories.getTargetRootBone();
        targetSubMenu = applyAccessories.getTargetSubMenu();
        associatedBlendShapesMap = applyAccessories.getAssociatedBlendShapes();
        associatedMeshRenderers = applyAccessories.getAssociatedMeshRenderers();
        femaleBlendShapeName = applyAccessories.getFemaleBlendShapeName();

        // Set up top level settings
        targetAvatarDescriptor = EditorGUILayout.ObjectField("Target Avatar Descriptor", targetAvatarDescriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
        targetSubMenu = EditorGUILayout.ObjectField("Target Submenu (Optional)", targetSubMenu, typeof(VRCExpressionsMenu), true) as VRCExpressionsMenu;
        targetNestArmature = EditorGUILayout.Toggle("Nest Armature", targetNestArmature);

        // Allow for setting the armature manually
        showOverrideArmature = EditorGUILayout.BeginFoldoutHeaderGroup(showOverrideArmature, "Override Default Armature (Optional)");
        if (showOverrideArmature)
        {
            targetArmature = EditorGUILayout.ObjectField("Target Armature", targetArmature, typeof(Transform), true) as Transform;
            targetRootBone = EditorGUILayout.ObjectField("Target Root Bone", targetRootBone, typeof(Transform), true) as Transform;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Determine whether or not the accessory include female blend shapes and apply them
        showFemaleBlendShapes = EditorGUILayout.BeginFoldoutHeaderGroup(showFemaleBlendShapes, "Set Female Blendshapes (Optional)");
        if (showFemaleBlendShapes)
        {
            targetFemaleAnimationClip = EditorGUILayout.ObjectField("Animation Clip", targetFemaleAnimationClip, typeof(AnimationClip), true) as AnimationClip;
            femaleBlendShapeName = EditorGUILayout.TextField("Blendshape Name", femaleBlendShapeName);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Determine if there are any other mesh renderers that interact with the accessories
        showAssociatedMeshRenderers = EditorGUILayout.BeginFoldoutHeaderGroup(showAssociatedMeshRenderers, "Set Associated Mesh Renderers (Optional)");
        if (showAssociatedMeshRenderers)
        {
            int numAssociatedMeshRenderers = EditorGUILayout.IntField("Length", associatedMeshRenderers.Length);
            if (numAssociatedMeshRenderers != associatedMeshRenderers.Length)
            {
                Array.Resize(ref associatedMeshRenderers, numAssociatedMeshRenderers);
                
            }
            
            for (int i = 0; i < numAssociatedMeshRenderers; i++)
            {
                associatedMeshRenderers[i] = EditorGUILayout.ObjectField($"Mesh Renderer #{i + 1}", associatedMeshRenderers[i], typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
                if (associatedMeshRenderers[i] != null && !associatedBlendShapesMap.ContainsKey(associatedMeshRenderers[i]))
                {
                    string[] blendShapes = {};
                    associatedBlendShapesMap[associatedMeshRenderers[i]] = blendShapes;
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (associatedMeshRenderers.Length != showPanelsForAssociatedMeshRenderers.Length)
        {
            Array.Resize(ref showPanelsForAssociatedMeshRenderers, associatedMeshRenderers.Length);
        }

        for (int i = 0; i < associatedMeshRenderers.Length; i++)
        {
            if (associatedMeshRenderers[i] != null && associatedBlendShapesMap.ContainsKey(associatedMeshRenderers[i]))
            {
                showPanelsForAssociatedMeshRenderers[i] = EditorGUILayout.BeginFoldoutHeaderGroup(showPanelsForAssociatedMeshRenderers[i], $"Blendshapes for Mesh Renderer #{i + 1}");
                string[] associatedBlendShapes = associatedBlendShapesMap[associatedMeshRenderers[i]];
                int numAssociatedBlendShapes = EditorGUILayout.IntField("Length", associatedBlendShapes.Length);
                Array.Resize(ref associatedBlendShapes, numAssociatedBlendShapes);

                for (int j = 0; j < numAssociatedBlendShapes; j++)
                {
                    associatedBlendShapes[j] = EditorGUILayout.TextField($"Blendshape #{j + 1}", associatedBlendShapes[j]);
                } 

                associatedBlendShapesMap[associatedMeshRenderers[i]] = associatedBlendShapes;
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        // Apply everything back to the main class
        applyAccessories.setTargetArmature(targetArmature);
        applyAccessories.setTargetAvatarDescriptor(targetAvatarDescriptor);
        applyAccessories.setTargetFemaleAnimationClip(targetFemaleAnimationClip);
        applyAccessories.setTargetNestArmature(targetNestArmature);
        applyAccessories.setTargetRootBone(targetRootBone);
        applyAccessories.setTargetSubMenu(targetSubMenu);
        applyAccessories.setAssociatedBlendShapes(associatedBlendShapesMap);
        applyAccessories.setAssociatedMeshRenderers(associatedMeshRenderers);
        applyAccessories.setFemaleBlendShapeName(femaleBlendShapeName);

        // Apply actual changes
        if (GUILayout.Button("Apply Changes"))
        {
            applyAccessories.Reassign();
        }
    }
}

[ExecuteInEditMode]
public class ApplyAccessories : MonoBehaviour
{
    private Transform targetArmature;
    private VRCAvatarDescriptor targetAvatarDescriptor;
    private VRCExpressionParameters targetAvatarParameters;
    private AnimationClip targetFemaleAnimationClip;
    private bool targetNestArmature;
    private Transform targetRootBone;
    private VRCExpressionsMenu targetSubMenu;
    private Dictionary<SkinnedMeshRenderer, string[]> associatedBlendShapes = new Dictionary<SkinnedMeshRenderer, string[]>();
    private SkinnedMeshRenderer[] associatedMeshRenderers = {};
   
    private string femaleBlendShapeName;

    public VRCAvatarDescriptor getTargetAvatarDescriptor()
    {
        return targetAvatarDescriptor;
    }

    public void setTargetAvatarDescriptor(VRCAvatarDescriptor target)
    {
        targetAvatarDescriptor = target;
    }

    public bool getTargetNestArmature()
    {
        return targetNestArmature;
    }

    public void setTargetNestArmature(bool target)
    {
        targetNestArmature = target;
    }

    public Dictionary<SkinnedMeshRenderer, string[]> getAssociatedBlendShapes()
    {
        return associatedBlendShapes;
    }

    public void setAssociatedBlendShapes(Dictionary<SkinnedMeshRenderer, string[]> blendShapes)
    {
        associatedBlendShapes = blendShapes;
    }

    public SkinnedMeshRenderer[] getAssociatedMeshRenderers()
    {
        return associatedMeshRenderers;
    }

    public void setAssociatedMeshRenderers(SkinnedMeshRenderer[] meshRenderers)
    {
        associatedMeshRenderers = meshRenderers;
    }

    public Transform getTargetArmature()
    {
        return targetArmature;
    }

    public void setTargetArmature(Transform target)
    {
        targetArmature = target;
    }

    public Transform getTargetRootBone()
    {
        return targetRootBone;
    }

    public void setTargetRootBone(Transform target)
    {
        targetRootBone = target;
    }

    public VRCExpressionsMenu getTargetSubMenu()
    {
        return targetSubMenu;
    }

    public void setTargetSubMenu(VRCExpressionsMenu target)
    {
        targetSubMenu = target;
    }

    public AnimationClip getTargetFemaleAnimationClip()
    {
        return targetFemaleAnimationClip;
    }

    public void setTargetFemaleAnimationClip(AnimationClip target)
    {
        targetFemaleAnimationClip = target;
    }

    public string getFemaleBlendShapeName()
    {
        return femaleBlendShapeName;
    }

    public void setFemaleBlendShapeName(string target)
    {
        femaleBlendShapeName = target;
    }

    AnimatorController getFXController()
    {
        VRCAvatarDescriptor.CustomAnimLayer fx = Array.Find(targetAvatarDescriptor.baseAnimationLayers,
            l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
        return fx.animatorController as AnimatorController;        
    }

    AnimationClip createAnimationClip(string targetName)
    {
        if(!AssetDatabase.IsValidFolder("Assets/GeneratedAnimations"))
        {    
            AssetDatabase.CreateFolder("Assets", "GeneratedAnimations");
        }
        AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/GeneratedAnimations/{targetName}.anim");
        AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f));
    
        if (!animationClip) {
            animationClip = new AnimationClip();
            animationClip.SetCurve(targetName, typeof(GameObject), "m_IsActive", animationCurve);
            AssetDatabase.CreateAsset(animationClip, $"Assets/GeneratedAnimations/{targetName}.anim");
        }
        else
        {
            animationClip.SetCurve(targetName, typeof(GameObject), "m_IsActive", animationCurve);
        }

        AnimationCurve blendShapeCurve = new AnimationCurve(new Keyframe(0.0f, 100.0f));
        foreach(SkinnedMeshRenderer renderer in associatedMeshRenderers)
        {
            string[] blendShapes = associatedBlendShapes[renderer];
            foreach(string blendShape in blendShapes)
            {
                animationClip.SetCurve(renderer.gameObject.name, typeof(SkinnedMeshRenderer), $"blendShape.{blendShape}", blendShapeCurve);
            }
        }

        AssetDatabase.SaveAssets();
        
        return animationClip;
    }

    void updateAnimatorController(string targetName)
    {
        AnimatorController animatorController = getFXController();
        AnimatorControllerLayer animatorControllerLayer;

        // If the parameter already exists, then we already created everything and this step is unnecessary.
        if (Array.Find(animatorController.parameters, (parameter) => { return parameter.name == targetName; } ) == null)
            animatorController.AddParameter(targetName, AnimatorControllerParameterType.Bool);
        else
            return;

        animatorControllerLayer = new AnimatorControllerLayer {
            defaultWeight = 1.0f,
            name = targetName,
            stateMachine = new AnimatorStateMachine()
        };
        animatorController.AddLayer(animatorControllerLayer);
        

        AnimatorStateMachine stateMachine = animatorControllerLayer.stateMachine;
        AnimatorState wait = stateMachine.AddState("Wait");
        AnimatorState target = stateMachine.AddState(targetName);

        AnimatorTransition defaultTransition = new AnimatorTransition {
            destinationState = wait,
        };
        AnimatorTransition[] entryTransitions = { defaultTransition };
        stateMachine.entryTransitions = entryTransitions;

        AnimatorCondition enableCondition = new AnimatorCondition {
            mode = AnimatorConditionMode.Equals,
            parameter = targetName,
            threshold = 1.0f
        };
        AnimatorCondition[] enableConditions = { enableCondition };
        AnimatorStateTransition enableTransition = new AnimatorStateTransition {
            conditions = enableConditions,
            destinationState = target,
            duration = 0.0f
        };
        wait.AddTransition(enableTransition);

        AnimatorCondition disableCondition = new AnimatorCondition {
            mode = AnimatorConditionMode.Equals,
            parameter = targetName,
            threshold = 0.0f
        };
        AnimatorCondition[] disableConditions = { disableCondition };
        AnimatorStateTransition disableTransition = new AnimatorStateTransition {
            conditions = disableConditions,
            destinationState = wait,
            duration = 0.0f
        };
        target.AddTransition(disableTransition);

        AnimationClip animationClip = createAnimationClip(targetName);
        target.motion = animationClip;
    }

    bool hasEnoughParameters(string[] targetNames, int required)
    {
        int usedBits = 0;
        foreach (VRCExpressionParameters.Parameter param in targetAvatarDescriptor.expressionParameters.parameters)
        {
            if (param != null && param.name != "" && !Array.Exists(targetNames, element => element == param.name))
            {
                usedBits += param.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            }
        }
        return usedBits <= (128 - required);
    }

    bool hasEnoughMenuControls(string[] targetNames, int required)
    {
        foreach (VRCExpressionsMenu.Control control in targetSubMenu.controls)
        {
            if (Array.Exists(targetNames, element => element == control.name))
            {
                required -= 1;
            }
        }
        return (8 - targetSubMenu.controls.Count) >= required;
    }

    VRCExpressionParameters.Parameter addParameterToExpressions(string targetName, int required)
    {
        VRCExpressionParameters.Parameter[] existingParameters = targetAvatarParameters.parameters;

        foreach (VRCExpressionParameters.Parameter parameter in existingParameters)
        {
            if (parameter.name == targetName)
            {
                return parameter;
            }
        }

        string[] targetNames = { targetName };

        VRCExpressionParameters.Parameter p = new VRCExpressionParameters.Parameter {
            name = targetName,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = 0.0f,
            saved = true
        };

        Array.Resize(ref existingParameters, existingParameters.Length + 1);
        existingParameters[existingParameters.Length - 1] = p;
        targetAvatarParameters.parameters = existingParameters;

        return p;
    }

    void addParameterToSubmenu(string targetName, int required)
    {
        foreach (VRCExpressionsMenu.Control control in targetSubMenu.controls)
        {
            if (control.name == targetName)
            {
                return;
            }
        }

        VRCExpressionsMenu.Control targetControl = new VRCExpressionsMenu.Control {
            name = targetName,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter {
                name = targetName
            }
        };
        targetSubMenu.controls.Add(targetControl);
    }

    void updateFemaleAnimationClip(string targetName)
    {
        AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0.0f, 100.0f));
        targetFemaleAnimationClip.SetCurve(targetName, typeof(SkinnedMeshRenderer), $"blendShape.{femaleBlendShapeName}", animationCurve);

        AssetDatabase.SaveAssets();
    }

    bool compareTransforms(Transform t1, Transform t2)
    {
        return (t1.position.Equals(t2.position) && t1.rotation.Equals(t2.rotation) && t1.localScale.Equals(t2.localScale));
    }

    [ContextMenu("Reassign Bones")]
    public void Reassign()
    {
        if (targetAvatarDescriptor == null) {
            Debug.Log("You need to assign a target avatar descriptor");
            return;
        }

        if (targetArmature == null)
        {
            targetArmature = targetAvatarDescriptor.transform.Find("Armature");
            if (targetArmature == null)
            {
                Debug.Log("Please manually specify the avatar armature");
                return;
            }
        }

        if (targetRootBone == null) {
            Transform[] children = targetArmature.GetComponentsInChildren<Transform>();
 
            bool found = false;
            foreach (Transform child in children)
            {
                if (child.parent == targetArmature)
                {
                    if (found)
                    {
                        Debug.Log("Please manually specify the avatar root bone");
                        return;
                    }
                    targetRootBone = child;
                    found = true;
                }
            }
            if (!found)
            {
                Debug.Log("Please manually specify the avatar root bone");
                return;
            }
        }

        try
        {
            PrefabUtility.UnpackPrefabInstance(this.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        catch (ArgumentException)
        {
            Debug.Log("Gameobject is already unpacked.");
        }

        targetAvatarParameters = targetAvatarDescriptor.expressionParameters;

        Debug.Log("Starting to reassign bones");
        SkinnedMeshRenderer[] meshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        string[] targetNames = {};
        Array.Resize(ref targetNames, meshRenderers.Length);

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            targetNames[i] = meshRenderers[i].name;
        }

        Debug.Log($"Processing {meshRenderers.Length} meshes");

        if (targetSubMenu != null && !hasEnoughParameters(targetNames, meshRenderers.Length))
        {
            Debug.LogError("Not enough available parameters to add all accessories. Remove the Submenu parameter to reassign armature and have accessories permanently on.");
            return;
        }

        if (targetSubMenu != null && !hasEnoughMenuControls(targetNames, meshRenderers.Length))
        {
            Debug.LogError("The selected submenu does not have enough control spots available to add all necessary controllers.");
            return;
        }

        foreach (SkinnedMeshRenderer meshRenderer in meshRenderers)
        {
            Debug.LogFormat("Starting to reassign bones for {0}", meshRenderer.gameObject.name);

            Transform[] sourceBones = meshRenderer.bones;
            Transform[] targetBones = targetArmature.GetComponentsInChildren<Transform>();

            for (int s = 0; s < sourceBones.Length; s++)
            {
                bool matched = false;
                for (int t = 0; t < targetBones.Length; t++)
                {
                    if (sourceBones[s].name.Contains(targetBones[t].name)) {
                        Component[] components = sourceBones[s].gameObject.GetComponents<Component>();
                        if ((components.Length > 1 || targetNestArmature) && (!compareTransforms(sourceBones[s], targetBones[t]) || sourceBones[s].name != targetBones[t].name))
                        {
                            sourceBones[s].parent = targetBones[t];
                        }
                        else
                        {
                            sourceBones[s] = targetBones[t];
                        }
                        
                        break;
                    }
                }
                if (!matched)
                {
                    string parentName = sourceBones[s].parent.name;
                    for (int t = 0; t < targetBones.Length; t++)
                    {
                        if (parentName.Contains(targetBones[t].name))
                        {
                            sourceBones[s].parent = targetBones[t];
                            break;
                        }
                    }
                }
            }

            meshRenderer.rootBone = targetRootBone;
            meshRenderer.bones = sourceBones;

            if (targetSubMenu != null)
            {
                addParameterToExpressions(meshRenderer.name, meshRenderers.Length);
                addParameterToSubmenu(meshRenderer.name, meshRenderers.Length);
                updateAnimatorController(meshRenderer.name);
                if (targetFemaleAnimationClip != null)
                    updateFemaleAnimationClip(meshRenderer.name);
                meshRenderer.gameObject.SetActive(false);
            }

            meshRenderer.gameObject.transform.parent = targetAvatarDescriptor.gameObject.transform;
        }

        DestroyImmediate(this.gameObject);
    }
}
