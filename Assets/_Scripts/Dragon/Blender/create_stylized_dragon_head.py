"""Build a stylized dragon head with one seven-second looping animation.

Blender: open this file in the Scripting workspace and press Run Script.
The dragon faces Blender -Y and contains no generated camera or light.
"""

import bpy
import math


COLLECTION_NAME = "Stylized_Dragon_Head"
FPS = 30
END_FRAME = 210  # 7.0 seconds: 3s idle, 3s mouth, then 1s still


def mat(name, color, metallic=0.0, roughness=0.55):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return material


RED = mat("Dragon_Red", (0.72, 0.035, 0.025), 0.0, 0.42)
RED_LIGHT = mat("Dragon_Red_Highlight", (1.0, 0.12, 0.055), 0.0, 0.38)
YELLOW = mat("Dragon_Yellow", (1.0, 0.57, 0.025), 0.0, 0.4)
ORANGE = mat("Dragon_Orange_Mane", (1.0, 0.28, 0.015), 0.0, 0.43)
CREAM = mat("Dragon_Teeth", (1.0, 0.91, 0.67), 0.0, 0.33)
BLACK = mat("Dragon_Black", (0.008, 0.006, 0.004), 0.0, 0.3)
EYE = mat("Dragon_Eye", (0.25, 0.95, 0.3), 0.0, 0.25)
MOUTH = mat("Dragon_Mouth", (0.18, 0.012, 0.018), 0.0, 0.52)


def clear_old():
    old = bpy.data.collections.get(COLLECTION_NAME)
    if old:
        for obj in list(old.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(old)
    collection = bpy.data.collections.new(COLLECTION_NAME)
    bpy.context.scene.collection.children.link(collection)
    return collection


COLLECTION = clear_old()


def move_to_collection(obj):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    COLLECTION.objects.link(obj)
    return obj


def finish(obj, name, material, location, scale, rotation=(0, 0, 0), parent=None, bevel=0.0):
    obj.name = name
    obj.location = location
    obj.scale = scale
    obj.rotation_euler = rotation
    if material:
        obj.data.materials.append(material)
    if bevel:
        modifier = obj.modifiers.new("Soft bevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
    if hasattr(obj.data, "polygons"):
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    if parent:
        obj.parent = parent
    return move_to_collection(obj)


def cube(name, material, location, scale, rotation=(0, 0, 0), parent=None, bevel=0.16):
    bpy.ops.mesh.primitive_cube_add()
    return finish(bpy.context.object, name, material, location, scale, rotation, parent, bevel)


def sphere(name, material, location, scale, parent=None):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1)
    return finish(bpy.context.object, name, material, location, scale, parent=parent)


def cone(name, material, location, radius, depth, rotation=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=radius, radius2=0.015, depth=depth)
    return finish(bpy.context.object, name, material, location, (1, 1, 1), rotation, parent)


def empty(name, location, parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.25
    obj.location = location
    obj.parent = parent
    COLLECTION.objects.link(obj)
    return obj


def fin(name, material, length, width, thickness, rotation=(0, 0, 0), parent=None):
    """Create a soft diamond/leaf-shaped mane fin extending along local +Z."""
    half_thickness = thickness * 0.5
    vertices = []
    profile = (
        (0.0, 0.0, 0.0),
        (-width, 0.0, length * 0.42),
        (0.0, 0.0, length),
        (width, 0.0, length * 0.42),
    )
    for y in (-half_thickness, half_thickness):
        vertices.extend((x, y, z) for x, _, z in profile)

    faces = (
        (0, 1, 2, 3),
        (7, 6, 5, 4),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    )
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    COLLECTION.objects.link(obj)
    return finish(obj, name, material, (0, 0, 0), (1, 1, 1), rotation, parent, 0.06)


root = empty("DragonHead_Root", (0, 0, 0))
# Animate this child instead of DragonHead_Root. Unity can otherwise interpret
# animation on the top-level transform as root motion and hide the visible sway.
head_motion = empty("HeadMotion", (0, 0, 0), root)
upper = empty("UpperJaw_Red", (0, 0.72, 1.65), head_motion)
lower = empty("LowerJaw_Yellow", (0, 0.72, 1.43), head_motion)

# Orange flexible mane/crest from the reference: a taller top leaf followed by
# broad rear and side leaves. Each leaf has its own pivot for secondary motion.
mane_specs = (
    ("Top", (0.0, 0.38, 2.66), 1.12, 0.38, (math.radians(-24), 0, 0)),
    ("Rear", (0.0, 1.02, 2.46), 1.28, 0.48, (math.radians(-58), 0, 0)),
    ("Left", (-0.78, 0.76, 2.25), 1.02, 0.42,
        (math.radians(-48), math.radians(-22), math.radians(18))),
    ("Right", (0.78, 0.76, 2.25), 1.02, 0.42,
        (math.radians(-48), math.radians(22), math.radians(-18))),
    ("BackLeft", (-0.58, 1.24, 1.92), 0.92, 0.38,
        (math.radians(-72), math.radians(-18), math.radians(22))),
    ("BackRight", (0.58, 1.24, 1.92), 0.92, 0.38,
        (math.radians(-72), math.radians(18), math.radians(-22))),
)
mane_pivots = []
for mane_name, location, length, width, rotation in mane_specs:
    pivot = empty(f"OrangeMane_{mane_name}_Pivot", location, upper)
    fin(f"OrangeMane_{mane_name}", ORANGE, length, width, 0.16,
        rotation=rotation, parent=pivot)
    mane_pivots.append(pivot)

# Broad wedge-like silhouette, readable from the Unity camera pitch.
cube("Upper_Snout", RED, (0, -0.45, 1.62), (1.38, 1.26, 0.38), parent=upper, bevel=0.25)
sphere("Upper_Cranium", RED, (0, 0.58, 2.02), (1.48, 1.10, 0.88), upper)
cube("Nose_Bridge", RED_LIGHT, (0, -1.25, 1.93), (0.73, 0.55, 0.23), parent=upper, bevel=0.18)
for side in (-1, 1):
    sphere(f"Cheek_{side}", RED, (side * 1.12, 0.05, 1.63), (0.47, 0.77, 0.58), upper)
    sphere(f"Brow_{side}", RED_LIGHT, (side * 0.68, 0.03, 2.45), (0.58, 0.42, 0.26), upper)
    sphere(f"EyeWhite_{side}", CREAM, (side * 0.67, -0.22, 2.28), (0.28, 0.22, 0.31), upper)
    sphere(f"Eye_{side}", EYE, (side * 0.67, -0.405, 2.28), (0.135, 0.07, 0.19), upper)
    sphere(f"Pupil_{side}", BLACK, (side * 0.67, -0.47, 2.28), (0.052, 0.035, 0.12), upper)
    cone(f"Horn_{side}", CREAM, (side * 0.82, 0.85, 2.73), 0.25, 1.05,
         (math.radians(-24), side * math.radians(19), side * math.radians(9)), upper)
    sphere(f"Nostril_{side}", BLACK, (side * 0.43, -1.76, 1.96), (0.105, 0.055, 0.07), upper)

# Dark mouth cavity hides intersections and makes the two jaw colors unmistakable.
cube("Mouth_Cavity", MOUTH, (0, -0.51, 1.34), (1.15, 1.13, 0.18), parent=upper, bevel=0.18)
cube("Lower_Jaw", YELLOW, (0, -0.46, 1.20), (1.29, 1.24, 0.30), parent=lower, bevel=0.23)
cube("Lower_Chin", YELLOW, (0, -1.22, 1.02), (0.88, 0.55, 0.26), parent=lower, bevel=0.18)

# Upper teeth point down; lower teeth point up. Keep the center open for a broad grin.
for index, x in enumerate((-0.98, -0.58, 0.58, 0.98)):
    cone(f"UpperTooth_{index}", CREAM, (x, -0.92, 1.31), 0.14, 0.46,
         (math.pi, 0, 0), upper)
for index, x in enumerate((-0.82, -0.4, 0.4, 0.82)):
    cone(f"LowerTooth_{index}", CREAM, (x, -0.91, 1.50), 0.12, 0.38, parent=lower)


def key_pose(frame, upper_x, lower_x, shake_z=0.0, shake_x=0.0):
    upper.rotation_euler = (math.radians(upper_x), 0, math.radians(shake_z))
    lower.rotation_euler = (math.radians(lower_x + shake_x), 0, math.radians(-shake_z * 0.65))
    upper.keyframe_insert("rotation_euler", frame=frame)
    lower.keyframe_insert("rotation_euler", frame=frame)


# Seconds 0-3: mouth stays neutral while the root plays idle.
key_pose(1, -2.0, 4.0)
key_pose(90, -2.0, 4.0)

# Seconds 3-6: open very wide -> trembling hold -> close.
key_pose(91, -2.0, 4.0)
key_pose(105, -16.0, 30.0)
for frame, z, x, upper_x in (
    (109, 0.85, 0.55, -16.8),
    (114, -0.75, -0.45, -15.4),
    (120, 0.65, 0.40, -16.6),
    (127, -0.58, -0.34, -15.5),
    (135, 0.50, 0.29, -16.5),
    (143, -0.42, -0.24, -15.6),
    (151, 0.34, 0.19, -16.35),
    (159, -0.25, -0.14, -15.75),
    (165, 0.0, 0.0, -16.0),
):
    key_pose(frame, upper_x, 30.0, z, x)
key_pose(180, -2.0, 4.0)
# Seconds 6-7: hold the neutral mouth completely still.
key_pose(210, -2.0, 4.0)

for obj in (upper, lower):
    if obj.animation_data and obj.animation_data.action:
        obj.animation_data.action.name = f"Dragon_IdleThenMouthStill_7s_{obj.name}"


def key_mane(frame, sway):
    """Animate staggered secondary motion across all orange mane leaves."""
    for index, pivot in enumerate(mane_pivots):
        side_sign = -1.0 if index % 2 else 1.0
        strength = 1.0 - index * 0.055
        pivot.rotation_euler = (
            math.radians(sway * 0.34 * strength),
            math.radians(sway * 0.22 * side_sign),
            math.radians(sway * 0.48 * side_sign * strength),
        )
        pivot.keyframe_insert("rotation_euler", frame=frame)


# Gentle idle sway, stronger reaction while the jaws open, then a still final
# second. Frame 1 and 210 match exactly for a clean looping animation.
for mane_frame, mane_sway in (
    (1, 0.0),
    (14, 3.0),
    (28, -3.4),
    (43, 2.8),
    (58, -2.5),
    (74, 1.8),
    (90, 0.0),
    (91, 0.0),
    (105, -4.5),
    (114, 6.5),
    (124, -6.0),
    (136, 5.2),
    (148, -4.4),
    (160, 3.2),
    (170, -1.8),
    (180, 0.0),
    (210, 0.0),
):
    key_mane(mane_frame, mane_sway)

for pivot in mane_pivots:
    if pivot.animation_data and pivot.animation_data.action:
        pivot.animation_data.action.name = f"Dragon_OrangeMane_7s_{pivot.name}"
        pivot.animation_data.action.use_fake_user = True


# Root idle occupies only seconds 0-3 and then remains neutral during the mouth
# section. The first and final poses match so the full six seconds loop cleanly.
def key_idle(frame, height, pitch, roll, sway=0.0, yaw=0.0):
    head_motion.location = (sway, 0.0, height)
    head_motion.rotation_euler = (
        math.radians(pitch),
        math.radians(yaw),
        math.radians(roll),
    )
    head_motion.keyframe_insert("location", frame=frame)
    head_motion.keyframe_insert("rotation_euler", frame=frame)


# Strong, clearly visible oscillation during the first 55 frames.
key_idle(1, 0.0, 0.0, 0.0, 0.0, 0.0)
key_idle(10, 0.02275, -0.385, 0.63, 0.0245, 0.42)
key_idle(19, -0.00525, 0.175, -0.56, -0.021, -0.35)
key_idle(28, 0.02975, -0.455, 0.7, 0.028, 0.455)
key_idle(37, -0.0063, 0.21, -0.665, -0.02625, -0.42)
key_idle(46, 0.0245, -0.4025, 0.6125, 0.02275, 0.385)
key_idle(55, 0.0, 0.1225, -0.49, -0.0175, -0.2975)

# Ease back to neutral before the mouth animation starts at frame 91.
key_idle(70, 0.014, -0.1925, 0.28, 0.0105, 0.175)
key_idle(90, 0.0, 0.0, 0.0)
key_idle(91, 0.0, 0.0, 0.0)
key_idle(180, 0.0, 0.0, 0.0)
# Hold the entire head still through the extra final second.
key_idle(210, 0.0, 0.0, 0.0)

if head_motion.animation_data and head_motion.animation_data.action:
    idle_action = head_motion.animation_data.action
    idle_action.name = "Dragon_IdleThenMouthStill_7s_HeadMotion"
    idle_action.use_fake_user = True


bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = END_FRAME
bpy.context.scene.render.fps = FPS
bpy.context.scene.render.engine = "BLENDER_EEVEE"
bpy.context.scene.render.resolution_x = 1080
bpy.context.scene.render.resolution_y = 1080
bpy.context.scene.render.resolution_percentage = 50
bpy.context.scene.world.color = (0.035, 0.045, 0.065)
bpy.context.scene.frame_set(1)


def verify_idle_motion():
    """Verify Blender actually evaluates visible motion in frames 2-50."""
    samples = []
    for frame in (1, 10, 28, 46, 50):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples.append((
            frame,
            head_motion.matrix_world.translation.copy(),
            tuple(head_motion.rotation_euler),
        ))

    start_position = samples[0][1]
    max_distance = max((position - start_position).length for _, position, _ in samples[1:])
    max_rotation = max(
        max(abs(value) for value in rotation)
        for _, _, rotation in samples[1:]
    )
    if max_distance < 0.015 or max_rotation < math.radians(0.4):
        raise RuntimeError("Idle verification failed: HeadMotion does not move visibly in frames 2-50.")

    print("Idle verification frames 1-50:")
    for frame, position, rotation in samples:
        degrees = tuple(round(math.degrees(value), 2) for value in rotation)
        print(f"  frame {frame}: position={tuple(round(v, 3) for v in position)}, rotation={degrees}")
    bpy.context.scene.frame_set(1)


verify_idle_motion()
print("Created one looping sequence: 3s idle + 3s mouth + 1s still, 210 frames at 30 fps.")
