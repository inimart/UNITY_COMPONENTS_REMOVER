using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Inimart.ComponentsRemover.Editor
{
    public class ComponentsRemover : EditorWindow
{
    private GameObject rootGO;
    private Dictionary<GameObject, List<Component>> gameObjectComponents = new Dictionary<GameObject, List<Component>>();
    private Dictionary<System.Type, int> componentTypeCounts = new Dictionary<System.Type, int>();
    private Vector2 scrollPosition;
    private Vector2 typesScrollPosition;
    private GUIStyle componentLabelStyle;
    private Dictionary<System.Type, Texture> componentIcons = new Dictionary<System.Type, Texture>();
    private bool needsRefresh = false;
    private Component componentToRemove = null;
    private GameObject componentOwner = null;
    private System.Type typeToRemove = null;
    private string filterComponents = "";
    
    // Special grouped types
    private static readonly System.Type COLLIDER_GROUP = typeof(Collider);
    private static readonly System.Type CUSTOM_MONOBEHAVIOUR_GROUP = typeof(MonoBehaviour);

    [MenuItem("Tools/Components Remover")]
    public static void ShowWindow()
    {
        GetWindow<ComponentsRemover>("Components Remover");
    }

    private void OnEnable()
    {
        LoadComponentIcons();
    }

    private void LoadComponentIcons()
    {
        componentIcons.Clear();
        
        componentIcons[typeof(MeshRenderer)] = EditorGUIUtility.IconContent("MeshRenderer Icon").image;
        componentIcons[typeof(MeshFilter)] = EditorGUIUtility.IconContent("MeshFilter Icon").image;
        componentIcons[typeof(Camera)] = EditorGUIUtility.IconContent("Camera Icon").image;
        componentIcons[typeof(Light)] = EditorGUIUtility.IconContent("Light Icon").image;
        componentIcons[typeof(AudioSource)] = EditorGUIUtility.IconContent("AudioSource Icon").image;
        componentIcons[typeof(Rigidbody)] = EditorGUIUtility.IconContent("Rigidbody Icon").image;
        componentIcons[typeof(Collider)] = EditorGUIUtility.IconContent("BoxCollider Icon").image;
        componentIcons[typeof(ParticleSystem)] = EditorGUIUtility.IconContent("ParticleSystem Icon").image;
        componentIcons[typeof(Animator)] = EditorGUIUtility.IconContent("Animator Icon").image;
        componentIcons[typeof(Canvas)] = EditorGUIUtility.IconContent("Canvas Icon").image;
        componentIcons[typeof(CanvasRenderer)] = EditorGUIUtility.IconContent("CanvasRenderer Icon").image;
    }

    private void OnGUI()
    {
        if (componentLabelStyle == null)
        {
            componentLabelStyle = new GUIStyle(EditorStyles.label);
            componentLabelStyle.richText = true;
        }

        // Handle component removal after GUI rendering
        if (Event.current.type == EventType.Layout && needsRefresh)
        {
            if (componentToRemove != null && componentOwner != null)
            {
                DoRemoveComponent(componentOwner, componentToRemove);
                componentToRemove = null;
                componentOwner = null;
            }
            else if (typeToRemove != null)
            {
                RemoveAllComponentsOfType(typeToRemove);
                typeToRemove = null;
            }
            needsRefresh = false;
            FindAllScripts();
        }

        EditorGUILayout.Space(10);
        
        EditorGUILayout.LabelField("Root GameObject", EditorStyles.boldLabel);
        rootGO = (GameObject)EditorGUILayout.ObjectField(rootGO, typeof(GameObject), true);
        
        EditorGUILayout.Space(10);
        
        EditorGUI.BeginDisabledGroup(rootGO == null);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Find All Components", GUILayout.Height(30)))
        {
            FindAllScripts();
        }
        
        if (GUILayout.Button("Remove All Components", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Remove All Components", 
                "Are you sure you want to remove ALL components from all GameObjects under " + 
                (rootGO != null ? rootGO.name : "root") + "?", "Yes", "No"))
            {
                RemoveAllComponents();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(10);
        
        // Display unique component types
        if (componentTypeCounts.Count > 0)
        {
            EditorGUILayout.LabelField($"Unique Component Types ({componentTypeCounts.Count}):", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            typesScrollPosition = EditorGUILayout.BeginScrollView(typesScrollPosition, GUILayout.MinHeight(200), GUILayout.MaxHeight(250));
            
            foreach (var kvp in componentTypeCounts.OrderBy(x => x.Key.Name))
            {
                System.Type type = kvp.Key;
                int count = kvp.Value;
                
                EditorGUILayout.BeginHorizontal();
                
                Texture icon = GetComponentIcon(type);
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                }
                
                string typeName = GetGroupedTypeName(type);
                EditorGUILayout.LabelField($"{typeName} ({count})", GUILayout.ExpandWidth(true));
                
                if (GUILayout.Button("Remove all of this type", GUILayout.Width(150)))
                {
                    if (EditorUtility.DisplayDialog("Remove All Components of Type", 
                        $"Are you sure you want to remove ALL {typeName} components from all GameObjects under {rootGO.name}?", 
                        "Yes", "No"))
                    {
                        typeToRemove = type;
                        needsRefresh = true;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
        }
        
        if (gameObjectComponents.Count > 0)
        {
            EditorGUILayout.LabelField($"Found {gameObjectComponents.Count} GameObjects with components:", EditorStyles.boldLabel);
            
            // Filter components field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter Components:", GUILayout.Width(100));
            filterComponents = EditorGUILayout.TextField(filterComponents, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                filterComponents = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var kvp in gameObjectComponents)
            {
                GameObject go = kvp.Key;
                List<Component> components = kvp.Value;
                
                if (go == null) continue;
                
                // Check if this GameObject should be displayed based on filter
                if (!string.IsNullOrEmpty(filterComponents))
                {
                    bool hasMatchingComponent = false;
                    foreach (Component comp in components)
                    {
                        if (comp != null && !(comp is Transform))
                        {
                            string componentName = comp.GetType().Name.ToLower();
                            if (componentName.StartsWith(filterComponents.ToLower()))
                            {
                                hasMatchingComponent = true;
                                break;
                            }
                        }
                    }
                    
                    if (!hasMatchingComponent)
                        continue;
                }
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<b>{go.name}</b>", componentLabelStyle);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = go;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel++;
                
                for (int i = 0; i < components.Count; i++)
                {
                    Component comp = components[i];
                    if (comp == null || comp is Transform) continue;
                    
                    // Highlight matching components
                    string componentName = comp.GetType().Name;
                    bool isMatching = string.IsNullOrEmpty(filterComponents) || 
                                    componentName.ToLower().StartsWith(filterComponents.ToLower());
                    
                    if (!isMatching)
                        continue;
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    Texture icon = GetComponentIcon(comp.GetType());
                    if (icon != null)
                    {
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                    }
                    
                    EditorGUILayout.LabelField(componentName);
                    
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        componentToRemove = comp;
                        componentOwner = go;
                        needsRefresh = true;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }

    private Texture GetComponentIcon(System.Type type)
    {
        if (componentIcons.ContainsKey(type))
            return componentIcons[type];
        
        foreach (var kvp in componentIcons)
        {
            if (type.IsSubclassOf(kvp.Key))
                return kvp.Value;
        }
        
        return EditorGUIUtility.IconContent("cs Script Icon").image;
    }

    private void FindAllScripts()
    {
        gameObjectComponents.Clear();
        componentTypeCounts.Clear();
        
        if (rootGO == null) return;
        
        Transform[] allTransforms = rootGO.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform transform in allTransforms)
        {
            GameObject go = transform.gameObject;
            if (go == null) continue;
            
            Component[] components = go.GetComponents<Component>();
            List<Component> nonTransformComponents = new List<Component>();
            
            foreach (Component comp in components)
            {
                if (comp != null && !(comp is Transform))
                {
                    nonTransformComponents.Add(comp);
                    
                    // Count component types with grouping
                    System.Type compType = comp.GetType();
                    System.Type typeKey = GetGroupedType(compType);
                    
                    if (!componentTypeCounts.ContainsKey(typeKey))
                    {
                        componentTypeCounts[typeKey] = 0;
                    }
                    componentTypeCounts[typeKey]++;
                }
            }
            
            if (nonTransformComponents.Count > 0)
            {
                gameObjectComponents[go] = nonTransformComponents;
            }
        }
        
        Repaint();
    }

    private void RemoveComponent(GameObject go, Component component)
    {
        componentToRemove = component;
        componentOwner = go;
        needsRefresh = true;
    }

    private void DoRemoveComponent(GameObject go, Component component)
    {
        if (component == null || go == null) return;
        
        Component[] allComponents = go.GetComponents<Component>();
        
        foreach (Component comp in allComponents)
        {
            if (comp == null || comp == component) continue;
            
            System.Type compType = comp.GetType();
            RequireComponent[] requireAttributes = (RequireComponent[])compType.GetCustomAttributes(typeof(RequireComponent), true);
            
            foreach (RequireComponent req in requireAttributes)
            {
                if ((req.m_Type0 != null && req.m_Type0 == component.GetType()) ||
                    (req.m_Type1 != null && req.m_Type1 == component.GetType()) ||
                    (req.m_Type2 != null && req.m_Type2 == component.GetType()))
                {
                    EditorUtility.DisplayDialog("Cannot Remove Component",
                        $"Cannot remove {component.GetType().Name} because it is required by {compType.Name}",
                        "OK");
                    return;
                }
            }
        }
        
        Undo.RecordObject(go, "Remove Component");
        DestroyImmediate(component);
    }

    private void RemoveAllComponents()
    {
        if (rootGO == null) return;
        
        FindAllScripts();
        
        List<GameObject> gameObjectsToProcess = new List<GameObject>(gameObjectComponents.Keys);
        
        foreach (GameObject go in gameObjectsToProcess)
        {
            if (go == null) continue;
            
            Component[] components = go.GetComponents<Component>();
            List<Component> componentsToRemove = new List<Component>();
            
            foreach (Component comp in components)
            {
                if (comp != null && !(comp is Transform))
                {
                    componentsToRemove.Add(comp);
                }
            }
            
            Undo.RecordObject(go, "Remove All Components");
            
            componentsToRemove.Sort((a, b) =>
            {
                if (HasRequireComponent(a.GetType()))
                    return 1;
                if (HasRequireComponent(b.GetType()))
                    return -1;
                return 0;
            });
            
            foreach (Component comp in componentsToRemove)
            {
                if (comp != null)
                {
                    DestroyImmediate(comp);
                }
            }
        }
        
        FindAllScripts();
    }

    private bool HasRequireComponent(System.Type type)
    {
        RequireComponent[] attrs = (RequireComponent[])type.GetCustomAttributes(typeof(RequireComponent), true);
        return attrs.Length > 0;
    }

    private void RemoveAllComponentsOfType(System.Type typeToRemove)
    {
        if (rootGO == null || typeToRemove == null) return;
        
        List<Component> componentsToRemove = new List<Component>();
        
        // Collect all components of the specified type or group
        foreach (var kvp in gameObjectComponents)
        {
            GameObject go = kvp.Key;
            List<Component> components = kvp.Value;
            
            foreach (Component comp in components)
            {
                if (comp != null)
                {
                    bool shouldRemove = false;
                    
                    if (typeToRemove == COLLIDER_GROUP)
                    {
                        // Remove all types of colliders
                        shouldRemove = comp is Collider;
                    }
                    else if (typeToRemove == CUSTOM_MONOBEHAVIOUR_GROUP)
                    {
                        // Remove all custom MonoBehaviours
                        shouldRemove = IsCustomMonoBehaviour(comp.GetType());
                    }
                    else
                    {
                        // Remove exact type match
                        shouldRemove = comp.GetType() == typeToRemove;
                    }
                    
                    if (shouldRemove)
                    {
                        componentsToRemove.Add(comp);
                    }
                }
            }
        }
        
        // Remove all collected components
        foreach (Component comp in componentsToRemove)
        {
            if (comp != null)
            {
                GameObject go = comp.gameObject;
                
                // Check if this component is required by others
                Component[] allComponents = go.GetComponents<Component>();
                bool canRemove = true;
                
                foreach (Component otherComp in allComponents)
                {
                    if (otherComp == null || otherComp == comp) continue;
                    
                    System.Type compType = otherComp.GetType();
                    RequireComponent[] requireAttributes = (RequireComponent[])compType.GetCustomAttributes(typeof(RequireComponent), true);
                    
                    foreach (RequireComponent req in requireAttributes)
                    {
                        if ((req.m_Type0 != null && req.m_Type0 == typeToRemove) ||
                            (req.m_Type1 != null && req.m_Type1 == typeToRemove) ||
                            (req.m_Type2 != null && req.m_Type2 == typeToRemove))
                        {
                            canRemove = false;
                            Debug.LogWarning($"Cannot remove {typeToRemove.Name} from {go.name} because it is required by {compType.Name}");
                            break;
                        }
                    }
                    
                    if (!canRemove) break;
                }
                
                if (canRemove)
                {
                    Undo.RecordObject(go, $"Remove {typeToRemove.Name}");
                    DestroyImmediate(comp);
                }
            }
        }
    }
    
    private System.Type GetGroupedType(System.Type type)
    {
        // Group all colliders together
        if (type.IsSubclassOf(typeof(Collider)) || type == typeof(Collider))
        {
            return COLLIDER_GROUP;
        }
        
        // Group all custom MonoBehaviours together
        if (IsCustomMonoBehaviour(type))
        {
            return CUSTOM_MONOBEHAVIOUR_GROUP;
        }
        
        // Return the original type for built-in components
        return type;
    }
    
    private string GetGroupedTypeName(System.Type type)
    {
        if (type == COLLIDER_GROUP)
        {
            return "All Colliders";
        }
        else if (type == CUSTOM_MONOBEHAVIOUR_GROUP)
        {
            return "Custom Scripts";
        }
        else
        {
            return type.Name;
        }
    }
    
    private bool IsCustomMonoBehaviour(System.Type type)
    {
        // Check if it's a MonoBehaviour but not a built-in Unity component
        if (!type.IsSubclassOf(typeof(MonoBehaviour)))
        {
            return false;
        }
        
        // Get the assembly name
        string assemblyName = type.Assembly.GetName().Name;
        
        // Unity's built-in components are in UnityEngine assemblies
        return !assemblyName.StartsWith("UnityEngine") && 
               !assemblyName.StartsWith("Unity.") &&
               !assemblyName.StartsWith("UnityEditor");
    }
}
}