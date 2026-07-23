"""Create a quad road from JSON exported by Unity's WayPointsRoadExporter.

In Blender: open Scripting workspace, open this file, set ROAD_JSON_PATH and Run Script.
"""

import bpy
import json
import os
from math import cos, pi, sin
from mathutils import Vector

# Change this to the JSON file exported from Unity.
ROAD_JSON_PATH = r"D:\Projects\Yarn Car Jam\Backups\WayPoints\waypoints_road.json"
# Leave empty to only create the mesh, or set an .fbx path for automatic Unity-ready export.
FBX_EXPORT_PATH = r""
ROAD_NAME = "WayPoints_Road"
ROAD_WIDTH = 2.5
TEXTURE_TILE_LENGTH = 2.0
# Number of edges in each semicircular end cap. Higher values make the caps smoother.
ROUND_CAP_SEGMENTS = 12


def unity_to_blender(point):
    # Unity Y-up/+Z-forward -> Blender Z-up/-Y-forward.
    # This removes the manual Rotation (90, 0, 180) and negative scale correction in Unity.
    return Vector((point["x"], -point["z"], point["y"]))


def delete_existing_road():
    existing = bpy.data.objects.get(ROAD_NAME)
    if existing is not None:
        bpy.data.objects.remove(existing, do_unlink=True)


def create_road():
    with open(ROAD_JSON_PATH, "r", encoding="utf-8") as file:
        exported = json.load(file)

    center_points = [unity_to_blender(point) for point in exported["splinePoints"]]
    is_loop = exported["loop"]
    if len(center_points) < 2:
        raise ValueError("The exported path needs at least two spline points.")

    vertices = []
    half_width = ROAD_WIDTH * 0.5
    point_count = len(center_points)
    for index, point in enumerate(center_points):
        if is_loop:
            previous = center_points[(index - 1) % point_count]
            next_point = center_points[(index + 1) % point_count]
        else:
            previous = center_points[max(index - 1, 0)]
            next_point = center_points[min(index + 1, point_count - 1)]

        tangent = next_point - previous
        tangent.z = 0.0
        if tangent.length_squared == 0.0:
            tangent = Vector((0.0, 1.0, 0.0))
        tangent.normalize()
        side = Vector((-tangent.y, tangent.x, 0.0)) * half_width
        vertices.extend([point - side, point + side])

    cumulative_lengths = [0.0]
    for index in range(1, point_count):
        cumulative_lengths.append(
            cumulative_lengths[-1] + (center_points[index] - center_points[index - 1]).length
        )

    total_length = cumulative_lengths[-1]
    if is_loop:
        total_length += (center_points[0] - center_points[-1]).length
        # Use an integer tile count so the UV phase also matches at the loop seam.
        repeat_count = max(1, round(total_length / TEXTURE_TILE_LENGTH))
        v_per_unit = repeat_count / total_length if total_length > 0.0 else 1.0
    else:
        v_per_unit = 1.0 / TEXTURE_TILE_LENGTH

    faces = []
    face_uvs = []
    segment_count = point_count if is_loop else point_count - 1
    for index in range(segment_count):
        next_index = (index + 1) % point_count
        # Winding produces upward (+Z) normals in Blender.
        faces.append((index * 2, index * 2 + 1, next_index * 2 + 1, next_index * 2))

        start_v = cumulative_lengths[index] * v_per_unit
        segment_length = (center_points[next_index] - center_points[index]).length
        end_v = (cumulative_lengths[index] + segment_length) * v_per_unit
        face_uvs.append(((0.0, start_v), (1.0, start_v), (1.0, end_v), (0.0, end_v)))

    if not is_loop:
        def append_round_cap(center, tangent, base_v, is_start):
            tangent = tangent.copy()
            tangent.z = 0.0
            if tangent.length_squared == 0.0:
                tangent = Vector((0.0, 1.0, 0.0))
            tangent.normalize()
            side_direction = Vector((-tangent.y, tangent.x, 0.0))
            outward = -tangent if is_start else tangent

            center_index = len(vertices)
            vertices.append(center.copy())
            arc_indices = []

            # Arc starts at point - side and ends at point + side, exactly matching
            # the U=0 and U=1 vertices of the first/last road cross-section.
            for step in range(ROUND_CAP_SEGMENTS + 1):
                angle = -pi * 0.5 + pi * step / ROUND_CAP_SEGMENTS
                offset = (
                    side_direction * (sin(angle) * half_width)
                    + outward * (cos(angle) * half_width)
                )
                arc_indices.append(len(vertices))
                vertices.append(center + offset)

            center_uv = (0.5, base_v)
            for step in range(ROUND_CAP_SEGMENTS):
                first_index = arc_indices[step]
                second_index = arc_indices[step + 1]
                first_offset = vertices[first_index] - center
                second_offset = vertices[second_index] - center

                def cap_uv(offset):
                    # Planar continuation of the road UVs. U keeps the same width
                    # scale and V extends beyond the endpoint by world distance.
                    return (
                        0.5 + offset.dot(side_direction) / ROAD_WIDTH,
                        base_v + offset.dot(tangent) * v_per_unit,
                    )

                first_uv = cap_uv(first_offset)
                second_uv = cap_uv(second_offset)

                # Match the winding used by the existing road strip.
                if is_start:
                    faces.append((center_index, first_index, second_index))
                    face_uvs.append((center_uv, first_uv, second_uv))
                else:
                    faces.append((center_index, second_index, first_index))
                    face_uvs.append((center_uv, second_uv, first_uv))

        start_tangent = center_points[1] - center_points[0]
        end_tangent = center_points[-1] - center_points[-2]
        append_round_cap(center_points[0], start_tangent, 0.0, True)
        append_round_cap(center_points[-1], end_tangent,
                         cumulative_lengths[-1] * v_per_unit, False)

    mesh = bpy.data.meshes.new(f"{ROAD_NAME}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()

    # U spans the road width. V is cumulative road distance, so textures tile seamlessly.
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for polygon, polygon_uvs in zip(mesh.polygons, face_uvs):
        for loop_index, uv in zip(polygon.loop_indices, polygon_uvs):
            uv_layer.data[loop_index].uv = uv

    road = bpy.data.objects.new(ROAD_NAME, mesh)
    bpy.context.collection.objects.link(road)
    return road


def export_fbx_for_unity(road):
    if not FBX_EXPORT_PATH:
        return

    export_path = os.path.abspath(FBX_EXPORT_PATH)
    if not export_path.lower().endswith(".fbx"):
        export_path += ".fbx"

    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.ops.object.select_all(action="DESELECT")
    road.select_set(True)
    bpy.context.view_layer.objects.active = road
    bpy.ops.export_scene.fbx(
        filepath=export_path,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        object_types={"MESH"},
    )
    print(f"Exported Unity-ready FBX to {export_path}")


delete_existing_road()
road_object = create_road()
export_fbx_for_unity(road_object)
print(f"Created {ROAD_NAME} from {ROAD_JSON_PATH}")
