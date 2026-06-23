#pragma once
#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/dictionary.hpp>
#include <godot_cpp/variant/utility_functions.hpp>

#include <atomic>
#include <thread>
#include <vector>

// Do not include <windows.h> in headers to avoid macro pollution in template-heavy code.

namespace godot {

class MediapipeBridge : public Node {
    GDCLASS(MediapipeBridge, Node);

public:
    MediapipeBridge();
    ~MediapipeBridge();

    bool init_hand(const String &model_path);
    bool init_holistic(const String &model_path);
    void start_camera(int64_t device_index, int64_t w, int64_t h, int64_t fps);
    void stop();

    // Signals: landmarks and gestures
    void emit_landmarks_updated(int hand_index, const PackedVector3Array &points);
    void emit_gestures_updated(const Dictionary &gestures);

protected:
    static void _bind_methods();

private:
#ifdef _WIN32
    void *hand_dll_ = nullptr;      // HMODULE, but kept as void* to keep windows.h out of headers
    void *holistic_dll_ = nullptr;  // HMODULE

    typedef int (*hand_init_t)(const wchar_t *graph_path);
    typedef int (*hand_detect_frame_direct_t)(unsigned char *bgra, int width, int height,
                                             double *out_array, int out_len);

    typedef int (*holistic_init_t)(const wchar_t *graph_path);
    typedef int (*holistic_detect_frame_direct_t)(unsigned char *bgra, int width, int height,
                                                  double *out_array, int out_len);

    hand_init_t hand_init_ = nullptr;
    hand_detect_frame_direct_t hand_detect_ = nullptr;
    holistic_init_t hol_init_ = nullptr;
    holistic_detect_frame_direct_t hol_detect_ = nullptr;
#endif

    std::thread worker_;
    std::atomic<bool> running_ { false };

    int cam_index_ = 0;
    int cam_w_ = 640;
    int cam_h_ = 480;
    int cam_fps_ = 30;

    String model_path_;
    bool hand_mode_ = true;
};

}