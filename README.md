# Components Remover

A Unity Editor tool for removing components from GameObjects in the scene hierarchy.

![](file:///H:/Projects3/UNITY_TOOLS/COMPONENTS_REMOVER/imgs/ComponentsRemover.png)

## How to Open

Menu → **Tools** → **Components Remover**

## Features

### 1. Select Root GameObject

- Drag a GameObject from the Scene Hierarchy into the "Root GameObject" field
- The tool will analyze this GameObject and all its children

### 2. Find All Components

- Click the **"Find All Components"** button
- Lists all GameObjects (including root) that have components besides Transform
- Each component is displayed with a distinctive icon based on its type

### 3. Individual Removal

- Click the **"X"** button next to a component to remove it
- If a component is required by others (via RequireComponent), a warning will appear

### 4. Remove All Components

- Click **"Remove All Components"** to remove all components from all GameObjects
- Confirmation will be requested before proceeding
- The operation supports Undo (Ctrl+Z)

## Notes

- Transform components are never removed
- All operations support Undo/Redo
- The "Select" button allows quick selection of a GameObject in the Hierarchy
