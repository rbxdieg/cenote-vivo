# Genera los activos `roca` y `roca2` con tres niveles de detalle (Decimate/Collapse)
# y los exporta a Assets/Models/Rocas.fbx con la convención de nombres _LODn de Unity.
# Uso: blender -b --factory-startup --python make_rocas.py
import bpy, bmesh, json, math, os

ROOT = r"C:\Users\Diego\ejercicios"
FBX_OUT = os.path.join(ROOT, "Assets", "Models", "Rocas.fbx")
BLEND_OUT = os.path.join(ROOT, "Tools", "blender", "rocas.blend")
RENDER_DIR = os.path.join(ROOT, "Tools", "renders")
TARGET_LOD0 = 9826
LOD_RATIOS = {1: 0.45, 2: 0.25}

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
os.makedirs(os.path.dirname(FBX_OUT), exist_ok=True)
os.makedirs(RENDER_DIR, exist_ok=True)


def tri_count(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    m = obj.evaluated_get(dg).to_mesh()
    m.calc_loop_triangles()
    n = len(m.loop_triangles)
    obj.evaluated_get(dg).to_mesh_clear()
    return n


def apply_modifiers(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def make_material():
    mat = bpy.data.materials.new("Mat_Roca")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.42, 0.40, 0.36, 1)
    bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def make_base_rock(name, seed, stretch):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=6, radius=1.0)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = stretch
    bpy.ops.object.transform_apply(scale=True)

    big = bpy.data.textures.new(f"{name}_big", 'VORONOI')
    big.noise_scale = 0.9
    big.distance_metric = 'DISTANCE'
    small = bpy.data.textures.new(f"{name}_small", 'CLOUDS')
    small.noise_scale = 0.25
    small.noise_depth = 4
    for tex, strength in ((big, 0.45), (small, 0.12)):
        d = obj.modifiers.new(tex.name, 'DISPLACE')
        d.texture = tex
        d.strength = strength
        d.texture_coords = 'OBJECT'
        d.mid_level = 0.5
    # Semilla: desplazar el origen del ruido para que roca y roca2 difieran.
    empty = bpy.data.objects.new(f"{name}_noise", None)
    scene.collection.objects.link(empty)
    empty.location = (seed * 3.1, seed * 1.7, seed * 2.3)
    for m in obj.modifiers:
        m.texture_coords_object = empty
        m.texture_coords = 'OBJECT'
    apply_modifiers(obj)
    bpy.data.objects.remove(empty)

    # Aplanar la base para que asiente en el suelo.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    zmin = min(v.co.z for v in bm.verts)
    floor = zmin + 0.18 * stretch[2]
    for v in bm.verts:
        if v.co.z < floor:
            v.co.z = floor
    bm.to_mesh(obj.data)
    bm.free()

    # Llevar el conteo base a ~9,826 triángulos (LOD0).
    dec = obj.modifiers.new("Decimate_Base", 'DECIMATE')
    dec.decimate_type = 'COLLAPSE'
    dec.ratio = TARGET_LOD0 / tri_count(obj)
    apply_modifiers(obj)

    # UV y sombreado suave.
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.shade_smooth()

    # Origen centrado en la base del objeto.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
    bm.free()
    scene.cursor.location = ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    obj.location = (0, 0, 0)
    return obj


def build_asset(name, seed, stretch, offset_x, mat):
    base = make_base_rock(f"{name}_LOD0", seed, stretch)
    base.data.materials.append(mat)
    parent = bpy.data.objects.new(name, None)
    scene.collection.objects.link(parent)
    parent.location = (offset_x, 0, 0)
    base.parent = parent

    stats = {"LOD0": {"ratio": 1.0, "tris": tri_count(base), "verts": len(base.data.vertices)}}
    for lvl, ratio in LOD_RATIOS.items():
        bpy.ops.object.select_all(action='DESELECT')
        base.select_set(True)
        bpy.context.view_layer.objects.active = base
        bpy.ops.object.duplicate()
        lod = bpy.context.active_object
        lod.name = f"{name}_LOD{lvl}"
        lod.data.name = lod.name
        dec = lod.modifiers.new("Decimate", 'DECIMATE')
        dec.decimate_type = 'COLLAPSE'
        dec.ratio = ratio
        dec.use_collapse_triangulate = True
        stats[f"LOD{lvl}"] = {"ratio": ratio, "tris": tri_count(lod)}
        apply_modifiers(lod)
        stats[f"LOD{lvl}"]["verts"] = len(lod.data.vertices)
        lod.parent = parent
    base.data.name = base.name
    return parent, stats


mat = make_material()
report = {}
_, report["roca"] = build_asset("roca", 1, (1.25, 1.0, 0.85), 0.0, mat)
_, report["roca2"] = build_asset("roca2", 7, (0.9, 1.1, 1.35), 4.0, mat)

# Escala unitaria en todo.
for o in scene.objects:
    if o.type == 'MESH':
        assert tuple(round(s, 4) for s in o.scale) == (1, 1, 1)

bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=FBX_OUT,
    use_selection=True,
    object_types={'EMPTY', 'MESH'},
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y',
    bake_space_transform=True,
    mesh_smooth_type='FACE',
    use_mesh_modifiers=True,
    add_leaf_bones=False,
)


# Evidencia: render de las tres LOD lado a lado, sólido + alámbrico.
def render_lods(name, path):
    for o in scene.objects:
        o.hide_render = not (o.name.startswith(name + "_LOD") and o.type == 'MESH')
    lods = sorted([o for o in scene.objects if o.name.startswith(name + "_LOD")], key=lambda o: o.name)
    parent = bpy.data.objects[name]
    saved = parent.location.copy()
    parent.location = (0, 0, 0)
    for i, o in enumerate(lods):
        o.location = ((i - 1) * 3.2, 0, 0)
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 10
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0, -12, 4.5)
    cam.rotation_euler = (math.radians(76), 0, 0)
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (0.62, 0.6, 0.56)
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 1800, 700
    scene.render.film_transparent = False
    # Sobreponer alambre con un modificador Wireframe en copias temporales.
    wires = []
    wmat = bpy.data.materials.get("Wire") or bpy.data.materials.new("Wire")
    wmat.diffuse_color = (0.05, 0.05, 0.05, 1)
    for o in lods:
        w = o.copy(); w.data = o.data.copy(); scene.collection.objects.link(w)
        w.parent = None; w.matrix_world = o.matrix_world.copy()
        w.data.materials.clear(); w.data.materials.append(wmat)
        mod = w.modifiers.new("wf", 'WIREFRAME'); mod.thickness = 0.008; mod.use_replace = True
        w.hide_render = False
        wires.append(w)
    scene.display.shading.color_type = 'MATERIAL'
    for o in lods:
        o.active_material.diffuse_color = (0.62, 0.6, 0.56, 1)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for w in wires:
        bpy.data.objects.remove(w)
    for o in lods:
        o.location = (0, 0, 0)
    parent.location = saved
    bpy.data.objects.remove(cam)


render_lods("roca", os.path.join(RENDER_DIR, "roca_lods_wire.png"))
render_lods("roca2", os.path.join(RENDER_DIR, "roca2_lods_wire.png"))

with open(os.path.join(ROOT, "Tools", "blender", "lod_report.json"), "w") as f:
    json.dump(report, f, indent=2)
print("REPORT " + json.dumps(report))
