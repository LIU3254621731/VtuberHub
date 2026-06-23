@tool
extends EditorImportPlugin

func _get_importer_name():
	return "VtuberHubAvatarPresetImporter"

func _get_visible_name():
	return "AvatarPreset (JSON)"

func _get_recognized_extensions():
	return ["avatar.json"]

func _get_save_extension():
	return "res"

func _get_resource_type():
	return "Resource" # We'll create AvatarPreset resource

func _get_preset_name():
	return "Default"

func _import(source_file, save_path, options, platform_variants, gen_files):
	var f := FileAccess.open(source_file, FileAccess.READ)
	if not f:
		push_error("Cannot open preset json: %s" % source_file)
		return ERR_CANT_OPEN
	var txt := f.get_as_text()
	var data = JSON.parse_string(txt)
	if typeof(data) != TYPE_DICTIONARY:
		push_error("Invalid JSON in %s" % source_file)
		return ERR_PARSE_ERROR
	var preset := load("res://avatar_preset.gd").new()
	if data.has("definition_path"):
		preset.definition = ResourceLoader.load(data["definition_path"]) # expect .tres of AvatarDefinition
	if data.has("wardrobe_selection"):
		preset.wardrobe_selection = data["wardrobe_selection"]
	if data.has("materials"):
		preset.materials = data["materials"]
	var out_path = "%s.%s" % [save_path, _get_save_extension()]
	var err = ResourceSaver.save(preset, out_path)
	return err