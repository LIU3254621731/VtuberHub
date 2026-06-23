extends Resource
class_name AvatarPreset

@export var definition: Resource # AvatarDefinition
# Selected wardrobe scene paths to attach
@export var wardrobe_selection: Array[String] = []
# Materials override to apply, e.g. {"skin_color": Color(1,0.85,0.8), "skin_tone": 0.5}
@export var materials: Dictionary = {}

func to_dict() -> Dictionary:
	return {
		"definition": definition,
		"wardrobe_selection": wardrobe_selection.duplicate(true),
		"materials": materials.duplicate(true)
	}