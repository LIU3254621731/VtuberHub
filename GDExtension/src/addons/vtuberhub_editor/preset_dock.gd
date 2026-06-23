@tool
extends Node

@onready var path_edit: LineEdit = %"PresetPath"
@onready var log: RichTextLabel = %"Log"

func _ready():
	(%"BtnBrowse" as Button).pressed.connect(_on_browse)
	(%"BtnApply" as Button).pressed.connect(_on_apply)

func _on_browse():
	var fd := FileDialog.new()
	fd.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	fd.access = FileDialog.ACCESS_RESOURCES
	fd.filters = ["*.tres,*.res ; AvatarPreset"]
	fd.file_selected.connect(func(p): path_edit.text = p)
	get_tree().root.add_child(fd)
	fd.popup_centered(Vector2(640, 420))

func _log(msg: String):
	log.append_bbcode("[color=cyan]" + msg + "[/color]\n")

func _get_selected_assembler():
	var ed := get_editor_interface()
	var sel = ed.get_selection().get_selected_nodes()
	if sel.size() == 0:
		return null
	return sel[0]

func _apply_preset_to(node: Node, preset: Resource):
	if not node:
		_log("No node selected.")
		return
	if not node.has_method("load_base"):
		_log("Selected node is not AvatarAssembler3D.")
		return
	if preset == null:
		_log("Preset is null.")
		return
	# Expect preset is AvatarPreset
	if preset.definition and preset.definition.base_scene_path:
		node.call("load_base", preset.definition.base_scene_path)
	# Attach wardrobe
	var arr: Array = []
	for p in preset.wardrobe_selection:
		arr.append({"path": p})
	node.call("attach_wardrobe", arr)
	# Materials
	node.call("apply_material_overrides", preset.materials)
	_log("Applied preset to %s" % node.name)

func _on_apply():
	var preset := ResourceLoader.load(path_edit.text)
	_apply_preset_to(_get_selected_assembler(), preset)