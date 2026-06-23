extends Control

@export var assembler_path: NodePath = NodePath("/root/Demo/AvatarAssembler3D")

@onready var _assembler := get_node(assembler_path)
@onready var _base_path: LineEdit = %"BasePath"
@onready var _wardrobe_paths: TextEdit = %"WardrobePaths"
@onready var _skin_color: ColorPickerButton = %"SkinColor"
@onready var _skin_tone: HSlider = %"SkinTone"
@onready var _preset_path: LineEdit = %"PresetPath"

func _ready():
	(%"BtnLoadBase" as Button).pressed.connect(_on_load_base)
	(%"BtnAttachWardrobe" as Button).pressed.connect(_on_attach_wardrobe)
	(%"BtnApplyMaterials" as Button).pressed.connect(_on_apply_materials)
	(%"BtnApplyPreset" as Button).pressed.connect(_on_apply_preset)

func _on_load_base():
	if _assembler and _assembler.has_method("load_base"):
		_assembler.call("load_base", _base_path.text)

func _on_attach_wardrobe():
	if _assembler and _assembler.has_method("attach_wardrobe"):
		var lines = _wardrobe_paths.text.split("\n", false)
		var arr: Array = []
		for p in lines:
			p = p.strip_edges()
			if p != "":
				arr.append({"path": p})
		_assembler.call("attach_wardrobe", arr)

func _on_apply_materials():
	if _assembler and _assembler.has_method("apply_material_overrides"):
		var params = {
			"skin_color": _skin_color.color,
			"skin_tone": _skin_tone.value
		}
		_assembler.call("apply_material_overrides", params)

func _on_apply_preset():
	var preset := ResourceLoader.load(_preset_path.text)
	if not preset:
		return
	if _assembler and _assembler.has_method("load_base"):
		if preset.definition and preset.definition.base_scene_path:
			_assembler.call("load_base", preset.definition.base_scene_path)
	if _assembler and _assembler.has_method("attach_wardrobe"):
		var arr: Array = []
		for p in preset.wardrobe_selection:
			arr.append({"path": p})
		_assembler.call("attach_wardrobe", arr)
	if _assembler and _assembler.has_method("apply_material_overrides"):
		_assembler.call("apply_material_overrides", preset.materials)
