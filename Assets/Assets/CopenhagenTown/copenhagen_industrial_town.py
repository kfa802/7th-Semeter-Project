"""
Copenhagen Industrialisation-Era Town — facade-only street set
================================================================

Builds a small row of Copenhagen street facades from the industrialisation
period (c. 1870-1910): a schoolhouse ("Skolen"), a workers' union hall
("Arbejdernes Fagforening"), a carpenter's workshop, a smithy and a plain
tenement house, standing along a cobbled street.

Only the FACADES are modelled (thin ~0.4 m deep slabs with layered
door/window/cornice/sign detail on top) — no interiors or roofs, like a
film-set street. Everything is built procedurally with Blender's own
Python API (bpy) — no external assets, textures or downloads are used, so
it runs fully offline.

HOW TO RUN
----------
Option A — inside Blender:
    1. Open Blender.
    2. Go to the "Scripting" workspace tab.
    3. Open this file (or paste its contents into a new text block).
    4. Click "Run Script" (or press Alt+P).
    The scene is built and automatically saved as a .blend file next to
    OUTPUT_PATH below (edit that path first if you want it saved
    elsewhere).

Option B — from a terminal, fully headless:
    blender --background --python copenhagen_industrial_town.py

Tested against the Blender 3.x / 4.x Python API. If a line errors out on
your specific Blender version, the console will show exactly which
material/node input name changed — the geometry-building functions below
are independent of that and will still have run.
"""

import bpy
import bmesh
import math
import mathutils
import os
import random

random.seed(7)

# ---------------------------------------------------------------------------
# Where to save the finished .blend file
# ---------------------------------------------------------------------------
OUTPUT_PATH = os.path.expanduser(
    "~/Documents/CopenhagenTown/copenhagen_industrial_town.blend"
)

# Coordinate convention used throughout this script:
#   X — runs along the street (left to right)
#   Z — up
#   Y — depth: y = 0 is the building line (the facade's outward face).
#       Positive y goes BACK into the (unmodelled) building.
#       Negative y comes FORWARD, toward the street / camera.
#       So all door/window/cornice/sign detail sits at small NEGATIVE y,
#       proud of the wall slab which occupies y in [0, WALL_DEPTH].

WALL_DEPTH = 0.4
FLOOR_H = 3.0
GROUND_H = 3.6
BUILDING_DEPTH = 9.0  # how far each building extends back from the street line


# ---------------------------------------------------------------------------
# Scene setup
# ---------------------------------------------------------------------------
def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for coll in list(bpy.data.collections):
        bpy.data.collections.remove(coll)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.curves,
        bpy.data.lights,
        bpy.data.cameras,
    ):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def new_collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def move_to_collection(obj, collection):
    if collection is None:
        return
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection.objects.link(obj)


# ---------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------
def make_material(name, color, roughness=0.85, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def make_brick_material(name, base_color):
    """A flat brick-tone material. (An earlier version used Blender's
    procedural Brick texture node here, but its Vector-input coordinate
    space kept fighting the Mortar/Scale settings and washed the whole
    facade out to near-white regardless of the chosen brick color — flat
    color is what actually renders reliably.)"""
    return make_material(name, base_color, roughness=0.92)


def build_materials():
    m = {}
    m["brick_yellow"] = make_brick_material("Brick_Yellow", (0.80, 0.60, 0.28))
    m["brick_red"] = make_brick_material("Brick_Red", (0.62, 0.18, 0.12))
    m["brick_redbrown"] = make_brick_material("Brick_RedBrown", (0.42, 0.20, 0.13))
    m["trim_white"] = make_material("Trim_White", (0.92, 0.90, 0.85), roughness=0.55)
    m["door_green"] = make_material("Door_Green", (0.09, 0.22, 0.16), roughness=0.4)
    m["door_navy"] = make_material("Door_Navy", (0.07, 0.10, 0.20), roughness=0.4)
    m["glass"] = make_material("Glass_Dark", (0.03, 0.05, 0.06), roughness=0.15, metallic=0.1)
    m["roof_slate"] = make_material("Roof_Slate", (0.12, 0.13, 0.15), roughness=0.7)
    m["iron_black"] = make_material("Iron_Black", (0.02, 0.02, 0.02), roughness=0.35, metallic=0.6)
    m["sign_gold"] = make_material("Sign_Gold", (0.55, 0.42, 0.12), roughness=0.3, metallic=0.7)
    m["street"] = make_material("Street_Cobble", (0.30, 0.29, 0.28), roughness=1.0)
    m["sidewalk"] = make_material("Sidewalk", (0.55, 0.53, 0.50), roughness=0.95)
    m["curb"] = make_material("Curb", (0.35, 0.34, 0.32), roughness=0.9)
    m["yard"] = make_material("Yard_Ground", (0.30, 0.38, 0.22), roughness=1.0)
    return m


# ---------------------------------------------------------------------------
# Geometry primitives
# ---------------------------------------------------------------------------
def add_box(name, base_center, size, material=None, rotation=(0, 0, 0), collection=None):
    """base_center=(x,y,z) is the CENTER in x/y and the BOTTOM in z.
    size=(w,d,h) are full dimensions."""
    w, d, h = size
    x, y, z = base_center
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + h / 2.0))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (w, d, h)
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material:
        obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return obj


def add_gable(name, base_center, width, depth, height, material=None, collection=None):
    """A triangular-prism roof gable / pediment sitting on top of a wall.
    base_center=(x, y, z) is the bottom-center of the FRONT triangle; the
    prism extends from y to y+depth."""
    x, y, z = base_center
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    bm = bmesh.new()
    v0 = bm.verts.new((x - width / 2.0, y, z))
    v1 = bm.verts.new((x + width / 2.0, y, z))
    v2 = bm.verts.new((x, y, z + height))
    v3 = bm.verts.new((x - width / 2.0, y + depth, z))
    v4 = bm.verts.new((x + width / 2.0, y + depth, z))
    v5 = bm.verts.new((x, y + depth, z + height))

    bm.faces.new((v0, v1, v2))
    bm.faces.new((v3, v5, v4))
    bm.faces.new((v0, v3, v4, v1))
    bm.faces.new((v0, v2, v5, v3))
    bm.faces.new((v1, v4, v5, v2))

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()

    if material:
        obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return obj


def add_text(name, location, body, size, extrude, material, collection, rotation=(math.radians(90), 0, 0)):
    bpy.ops.object.text_add(location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.body = body
    obj.data.size = size
    obj.data.extrude = extrude
    obj.data.align_x = 'CENTER'
    obj.data.align_y = 'CENTER'
    obj.rotation_euler = rotation
    if material:
        obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return obj


# ---------------------------------------------------------------------------
# Building details
# ---------------------------------------------------------------------------
def add_window(coll, mats, cx, z_bottom, w, h):
    add_box("sill", (cx, -0.06, z_bottom - 0.08), (w + 0.22, 0.14, 0.08), mats["trim_white"], collection=coll)
    add_box("frame", (cx, -0.05, z_bottom), (w + 0.12, 0.10, h), mats["trim_white"], collection=coll)
    add_box("lintel", (cx, -0.06, z_bottom + h), (w + 0.22, 0.14, 0.09), mats["trim_white"], collection=coll)
    # NOTE on depth ordering: each box's *front face* is center_y - depth/2,
    # not center_y itself. "frame" is 0.10 deep centered at -0.05, so its
    # front face already reaches y=-0.10 — deeper than it looks. "glass" and
    # "mullion" must clear that front face (with margin) or the frame's own
    # bulk will hide them, even though their *centers* look further out.
    add_box("glass", (cx, -0.105, z_bottom + 0.03), (w - 0.06, 0.03, h - 0.06), mats["glass"], collection=coll)
    add_box("mullion_v", (cx, -0.13, z_bottom + 0.03), (0.04, 0.02, h - 0.06), mats["trim_white"], collection=coll)
    add_box("mullion_h", (cx, -0.13, z_bottom + h * 0.5), (w - 0.06, 0.02, 0.04), mats["trim_white"], collection=coll)


def add_door(coll, mats, cx, w, h, door_mat, fanlight=True):
    # See the depth-ordering note in add_window: each later element must
    # clear the *front face* (center - depth/2) of the one behind it.
    add_box("door_frame", (cx, -0.06, 0), (w + 0.18, 0.12, h + 0.12), mats["trim_white"], collection=coll)
    add_box("door_leaf", (cx, -0.11, 0), (w, 0.06, h), door_mat, collection=coll)
    add_box("door_panel_l", (cx - w * 0.22, -0.15, h * 0.12), (w * 0.32, 0.02, h * 0.35), door_mat, collection=coll)
    add_box("door_panel_r", (cx + w * 0.05, -0.15, h * 0.12), (w * 0.32, 0.02, h * 0.35), door_mat, collection=coll)
    if fanlight:
        add_box("fanlight", (cx, -0.11, h), (w, 0.06, 0.32), mats["glass"], collection=coll)
    add_box("step", (cx, -0.45, 0), (w + 0.7, 0.55, 0.09), mats["curb"], collection=coll)


def add_sign(coll, mats, cx, z, text, size=0.32):
    plaque_w = max(len(text) * size * 0.62 + 0.4, 1.0)
    add_box("sign_plaque", (cx, -0.055, z - 0.28), (plaque_w, 0.05, size + 0.5), mats["trim_white"], collection=coll)
    add_text("sign_" + text.split(" ")[0][:10], (cx, -0.09, z), text, size, 0.012, mats["sign_gold"], coll)


# ---------------------------------------------------------------------------
# Facade builder
# ---------------------------------------------------------------------------
def build_facade(x0, name, width, floors, cols, wall_key, sign, pediment, door_w, shopfront, mats):
    coll = new_collection(name)
    total_h = GROUND_H + floors * FLOOR_H
    cx = x0 + width / 2.0
    wall_mat = mats[wall_key]
    door_mat = mats["door_green"] if floors >= 3 else mats["door_navy"]

    # Main wall slab (y in [0, WALL_DEPTH], i.e. sitting *behind* the y=0
    # building line so all detail below can protrude toward the camera).
    add_box("wall", (cx, WALL_DEPTH / 2.0, 0), (width, WALL_DEPTH, total_h), wall_mat, collection=coll)

    # Plinth / dark base band
    add_box("plinth", (cx, -0.02, 0), (width + 0.05, 0.10, 0.55), mats["curb"], collection=coll)

    # Upper floor windows
    win_w, win_h = 1.1, 1.7
    margin = 0.9
    usable = max(width - 2 * margin, 0.1)
    for f in range(1, floors + 1):
        z0 = GROUND_H + (f - 1) * FLOOR_H + 0.65
        if cols <= 1:
            xs = [cx]
        else:
            xs = [x0 + margin + usable * i / (cols - 1) for i in range(cols)]
        for wx in xs:
            add_window(coll, mats, wx, z0, win_w, win_h)
        z_belt = GROUND_H + (f - 1) * FLOOR_H
        add_box("belt", (cx, -0.05, z_belt), (width + 0.1, 0.10, 0.10), mats["trim_white"], collection=coll)

    # Ground floor: door (+ shop window or flanking windows)
    if shopfront:
        door_cx = x0 + door_w / 2.0 + 0.5
        add_door(coll, mats, door_cx, door_w, 2.3, door_mat)
        shop_x0 = door_cx + door_w / 2.0 + 0.55
        shop_w = max((x0 + width) - shop_x0 - 0.45, 0.6)
        shop_cx = shop_x0 + shop_w / 2.0
        add_box("shop_frame", (shop_cx, -0.06, 0.1), (shop_w, 0.12, 2.4), mats["trim_white"], collection=coll)
        add_box("shop_glass", (shop_cx, -0.12, 0.22), (max(shop_w - 0.24, 0.3), 0.04, 2.05), mats["glass"], collection=coll)
        add_box("shop_mullion", (shop_cx, -0.145, 0.22), (0.05, 0.03, 2.05), mats["trim_white"], collection=coll)
    else:
        add_door(coll, mats, cx, door_w, 2.4, door_mat)
        offset = width / 2.0 - 1.3
        if offset > 1.0:
            add_window(coll, mats, cx - offset, 0.7, win_w * 1.05, win_h * 1.05)
            add_window(coll, mats, cx + offset, 0.7, win_w * 1.05, win_h * 1.05)

    # Cornice along the top
    add_box("cornice", (cx, -0.08, total_h), (width + 0.35, 0.22, 0.28), mats["trim_white"], collection=coll)

    # Roofline: pediment for civic buildings, plain parapet otherwise
    if pediment:
        add_gable("pediment", (cx, 0.0, total_h + 0.28), width * 0.42, WALL_DEPTH, 1.7, mats["trim_white"], collection=coll)
    else:
        add_box("parapet", (cx, 0.05, total_h + 0.28), (width * 0.88, 0.18, 0.35), mats["trim_white"], collection=coll)
        if random.random() > 0.4:
            chimney_x = cx + random.uniform(-width * 0.3, width * 0.3)
            add_box("chimney", (chimney_x, 0.15, total_h + 0.3), (0.4, 0.4, 0.9), wall_mat, collection=coll)

    # School gets a small bell cupola on the ridge
    if name == "Skolen":
        add_box("cupola_base", (cx, 0.0, total_h + 1.98), (0.6, 0.6, 0.5), mats["trim_white"], collection=coll)
        bpy.ops.mesh.primitive_cone_add(radius1=0.45, depth=0.7, location=(cx, -0.1, total_h + 2.68))
        cone = bpy.context.active_object
        cone.name = "cupola_roof"
        cone.data.materials.append(mats["roof_slate"])
        move_to_collection(cone, coll)

    # Sign
    if sign:
        add_sign(coll, mats, cx, 2.85, sign)

    # ---- Give the building real volume, not just a front facade ----
    # Side/back walls must reach all the way up to the underside of the roof
    # slab below — if they stopped at total_h and the roof sat higher (to
    # clear the cornice/pediment), there'd be an open gap all along the
    # sides that you could see straight through into a hollow shell.
    roof_z = total_h + (0.5 if pediment else 0.65)
    wall_top = roof_z

    # Back wall (plain brick, closes off the far end of the building).
    add_box("back_wall", (cx, BUILDING_DEPTH, 0), (width, WALL_DEPTH, wall_top), wall_mat, collection=coll)
    # Side walls (plain brick, run from the street line to the back wall).
    # size=(x-width, y-depth, z-height): thin in X, long in Y — no rotation needed.
    add_box("side_wall_l", (x0, BUILDING_DEPTH / 2.0, 0),
            (WALL_DEPTH, BUILDING_DEPTH + WALL_DEPTH, wall_top), wall_mat, collection=coll)
    add_box("side_wall_r", (x0 + width, BUILDING_DEPTH / 2.0, 0),
            (WALL_DEPTH, BUILDING_DEPTH + WALL_DEPTH, wall_top), wall_mat, collection=coll)

    # Flat slate roof capping the building. Pedimented civic buildings keep
    # their pediment/cupola out front (y in [0, ~0.4]) and get the roof only
    # over the depth behind it, so the two don't clip into each other.
    if pediment:
        rear_y0 = WALL_DEPTH + 0.3
        rear_depth = (BUILDING_DEPTH + WALL_DEPTH) - rear_y0
        add_box("roof", (cx, rear_y0 + rear_depth / 2.0, roof_z),
                (width + 0.3, rear_depth + 0.3, 0.25), mats["roof_slate"], collection=coll)
    else:
        add_box("roof", (cx, BUILDING_DEPTH / 2.0, roof_z),
                (width + 0.3, BUILDING_DEPTH + 0.3, 0.25), mats["roof_slate"], collection=coll)

    return coll, total_h


# ---------------------------------------------------------------------------
# Street furniture
# ---------------------------------------------------------------------------
def build_ground(total_width, mats):
    coll = new_collection("Street")
    cx = total_width / 2.0
    add_box("street", (cx, -6.0, -0.16), (total_width + 8, 6.0, 0.16), mats["street"], collection=coll)
    add_box("sidewalk", (cx, -1.4, -0.06), (total_width + 8, 2.4, 0.09), mats["sidewalk"], collection=coll)
    add_box("curb", (cx, -2.55, -0.02), (total_width + 8, 0.16, 0.14), mats["curb"], collection=coll)
    # A walkable yard behind the buildings, and narrow paths down each side,
    # so the whole block can be walked around, not just the street frontage.
    back_y = BUILDING_DEPTH + WALL_DEPTH
    add_box("yard", (cx, back_y + 4.0, -0.06), (total_width + 8, 8.0, 0.06), mats["yard"], collection=coll)
    side_len = back_y + 4.0
    add_box("side_path_l", (-2.5, side_len / 2.0, -0.06), (3.0, side_len, 0.06), mats["yard"], collection=coll)
    add_box("side_path_r", (total_width + 2.5, side_len / 2.0, -0.06), (3.0, side_len, 0.06), mats["yard"], collection=coll)
    return coll


def add_lamp_post(coll, mats, x, y=-2.0):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=3.2, location=(x, y, 1.6))
    pole = bpy.context.active_object
    pole.name = "lamp_pole"
    pole.data.materials.append(mats["iron_black"])
    move_to_collection(pole, coll)

    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.16, location=(x, y, 3.3))
    lamp = bpy.context.active_object
    lamp.name = "lamp_head"
    lamp.data.materials.append(mats["trim_white"])
    move_to_collection(lamp, coll)

    bpy.ops.mesh.primitive_cone_add(radius1=0.22, depth=0.25, location=(x, y, 3.55))
    cap = bpy.context.active_object
    cap.name = "lamp_cap"
    cap.data.materials.append(mats["iron_black"])
    move_to_collection(cap, coll)


# ---------------------------------------------------------------------------
# Camera & lighting
# ---------------------------------------------------------------------------
def setup_camera_and_light(total_width, total_height):
    cam_data = bpy.data.cameras.new("StreetCam")
    cam_data.lens = 24
    cam_obj = bpy.data.objects.new("StreetCam", cam_data)
    bpy.context.scene.collection.objects.link(cam_obj)
    cam_obj.location = (total_width / 2.0, -18.0, total_height * 0.42)
    cam_obj.rotation_euler = (math.radians(90), 0, 0)

    # A second, elevated 3/4 camera that actually shows the building volumes
    # and roofs, not just the street-facing facades.
    aerial_data = bpy.data.cameras.new("AerialCam")
    aerial_data.lens = 22
    aerial_obj = bpy.data.objects.new("AerialCam", aerial_data)
    bpy.context.scene.collection.objects.link(aerial_obj)
    aerial_obj.location = (total_width * 0.82, -total_width * 0.55, total_height * 2.0)
    direction = mathutils.Vector((total_width / 2.0, BUILDING_DEPTH / 2.0, 0)) - aerial_obj.location
    aerial_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    bpy.context.scene.camera = aerial_obj

    sun_data = bpy.data.lights.new("Sun", type='SUN')
    sun_data.energy = 3.2
    sun_data.angle = math.radians(2.0)
    sun_obj = bpy.data.objects.new("Sun", sun_data)
    bpy.context.scene.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = (math.radians(55), 0, math.radians(35))

    fill_data = bpy.data.lights.new("Fill", type='AREA')
    fill_data.energy = 150
    fill_data.size = 8
    fill_obj = bpy.data.objects.new("Fill", fill_data)
    bpy.context.scene.collection.objects.link(fill_obj)
    fill_obj.location = (total_width / 2.0, -10.0, 6.0)
    fill_obj.rotation_euler = (math.radians(60), 0, 0)

    world = bpy.data.worlds.get("World")
    if world is None:
        world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.72, 0.78, 0.85, 1.0)
        bg.inputs[1].default_value = 1.0

    try:
        bpy.context.scene.render.engine = 'CYCLES'
    except Exception:
        pass


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    clear_scene()
    mats = build_materials()

    specs = [
        dict(name="Skolen", width=9.0, floors=2, cols=4, wall_key="brick_yellow",
             sign="SKOLE", pediment=True, door_w=1.6, shopfront=False),
        dict(name="Fagforeningen", width=7.5, floors=3, cols=3, wall_key="brick_red",
             sign="ARBEJDERNES FAGFORENING", pediment=True, door_w=1.4, shopfront=False),
        dict(name="Snedkervaerksted", width=5.5, floors=2, cols=2, wall_key="brick_redbrown",
             sign="SNEDKER", pediment=False, door_w=1.7, shopfront=True),
        dict(name="Smedjen", width=5.0, floors=2, cols=2, wall_key="brick_red",
             sign="SMEDJE", pediment=False, door_w=1.7, shopfront=True),
        dict(name="Beboelseshus", width=6.5, floors=3, cols=3, wall_key="brick_yellow",
             sign=None, pediment=False, door_w=1.3, shopfront=False),
    ]

    gap = 0.1
    cursor_x = 0.0
    max_h = 0.0
    for spec in specs:
        spec = dict(spec)
        x0 = cursor_x
        wall_key = spec.pop("wall_key")
        _, h = build_facade(x0, mats=mats, wall_key=wall_key, **spec)
        max_h = max(max_h, h)
        cursor_x += spec["width"] + gap

    total_width = cursor_x - gap
    street_coll = build_ground(total_width, mats)

    lamp_positions = [1.0, total_width * 0.35, total_width * 0.65, total_width - 1.0]
    for lx in lamp_positions:
        add_lamp_post(street_coll, mats, lx)

    setup_camera_and_light(total_width, max_h)

    os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_PATH)
    print("Saved Copenhagen industrial-era street to:", OUTPUT_PATH)


if __name__ == "__main__":
    main()
