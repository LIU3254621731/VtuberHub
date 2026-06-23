extends Resource
class_name AvatarDefinition

@export var base_scene_path: String = ""
# Each entry: { "category": String, "path": String }
@export var wardrobe_items: Array[Dictionary] = []
# Material slots default values, e.g. {"skin_color": Color(1,0.85,0.8), "skin_tone": 0.5}
@export var material_defaults: Dictionary = {}

func to_dict() -> Dictionary:
	return {
		"base_scene_path": base_scene_path,
		"wardrobe_items": wardrobe_items.duplicate(true),
		"material_defaults": material_defaults.duplicate(true)
	}