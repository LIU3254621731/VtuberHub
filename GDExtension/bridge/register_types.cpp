// Godot 4.x GDExtension entry and class registration
#include <gdextension_interface.h>
#include <godot_cpp/godot.hpp>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/classes/node3d.hpp>

#include "mediapipe_bridge.h"
#include "humanoid_mapper_3d.h"
#include "avatar_assembler_3d.h"

using namespace godot;

static void initialize_vtuberhub_module(ModuleInitializationLevel p_level) {
    if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
        return;
    }
    ClassDB::register_class<MediapipeBridge>();
    ClassDB::register_class<HumanoidMapper3D>();
    ClassDB::register_class<AvatarAssembler3D>();
}

static void uninitialize_vtuberhub_module(ModuleInitializationLevel p_level) {
    if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
        return;
    }
}

extern "C" {
GDExtensionBool GDExtensionInit(GDExtensionInterfaceGetProcAddress p_get_proc_address,
                                GDExtensionClassLibraryPtr p_library,
                                GDExtensionInitialization *r_initialization) {
    GDExtensionBinding::InitObject init_obj(p_get_proc_address, p_library, r_initialization);

    init_obj.register_initializer(initialize_vtuberhub_module);
    init_obj.register_terminator(uninitialize_vtuberhub_module);

    init_obj.set_minimum_library_initialization_level(MODULE_INITIALIZATION_LEVEL_SCENE);
    return init_obj.init();
}
}