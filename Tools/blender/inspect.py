import bpy, json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"C:\Users\Diego\ejercicios\Assets\Scenes\CENOTE.fbx")
for o in bpy.data.objects:
    d={"name":o.name,"type":o.type,"parent":o.parent.name if o.parent else None,"scale":[round(v,3) for v in o.scale],"loc":[round(v,2) for v in o.location],"dims":[round(v,2) for v in o.dimensions]}
    if o.type=='MESH':
        m=o.data; m.calc_loop_triangles(); d["tris"]=len(m.loop_triangles); d["verts"]=len(m.vertices)
        d["mats"]=[s.material.name if s.material else None for s in o.material_slots]
    print("OBJ", json.dumps(d))
