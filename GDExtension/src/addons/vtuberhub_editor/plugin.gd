@tool
extends EditorPlugin

var _importer
var _dock

func _enter_tree():
	# Register importer
	_importer = preload("res://addons/vtuberhub_editor/preset_importer.gd").new()
	add_import_plugin(_importer)
	# Add simple dock for one-click apply
	_dock = preload("res://addons/vtuberhub_editor/preset_dock.tscn").instantiate()
	add_control_to_dock(DOCK_SLOT_RIGHT_UL, _dock)

func _exit_tree():
	if _importer:
		remove_import_plugin(_importer)
		_importer = null
	if _dock:
		remove_control_from_docks(_dock)
		_dock.free()
		_dock = null