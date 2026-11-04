# Renderiza evidencia de cada nivel LOD (sólido + alámbrico) a partir de rocas.blend.
# Uso: blender -b rocas.blend --python render_lods.py
import bpy, math, os

RENDER_DIR = r"C:\Users\Diego\ejercicios\Tools\renders"
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading
sh.light = 'STUDIO'
sh.color_type = 'OBJECT'
sh.show_cavity = True
scene.render.resolution_x, scene.render.resolution_y = 900, 700
world = bpy.data.worlds.new("W"); scene.world = world
world.color = (0.93, 0.93, 0.93)
sh.background_type = 'WORLD'

cam_data = bpy.data.cameras.new("cam"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 3.4
cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam)
scene.camera = cam

for name in ("roca", "roca2"):
    parent = bpy.data.objects[name]
    parent.location = (0, 0, 0)
    bpy.context.view_layer.update()
    for lvl in range(3):
        target = bpy.data.objects[f"{name}_LOD{lvl}"]
        for o in scene.objects:
            o.hide_render = o.type == 'MESH' and o is not target
        target.color = (0.66, 0.63, 0.58, 1)
        wire = target.copy(); wire.data = target.data.copy()
        scene.collection.objects.link(wire)
        wire.parent = None
        wire.matrix_world = target.matrix_world.copy()
        wire.color = (0.08, 0.08, 0.08, 1)
        mod = wire.modifiers.new("wf", 'WIREFRAME'); mod.thickness = 0.004; mod.use_replace = True
        wire.hide_render = False
        h = target.dimensions.z
        cam.location = (0, -6, h * 0.5 + 2.2)
        cam.rotation_euler = (math.radians(70), 0, 0)
        scene.render.filepath = os.path.join(RENDER_DIR, f"{name}_LOD{lvl}.png")
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(wire)
print("DONE")
