#include "mediapipe_bridge.h"
#include <godot_cpp/classes/os.hpp>
#include <godot_cpp/classes/engine.hpp>
#include <godot_cpp/classes/object.hpp>
#include <godot_cpp/classes/image.hpp>
#include <godot_cpp/classes/image_texture.hpp>
#include <godot_cpp/variant/callable.hpp>

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

#ifdef VTUBERHUB_USE_OPENCV
#include <opencv2/opencv.hpp>
#endif

using namespace godot;

MediapipeBridge::MediapipeBridge() {}
MediapipeBridge::~MediapipeBridge() { stop(); }

void MediapipeBridge::_bind_methods() {
    // Temporarily disable all method bindings to avoid MethodBind template issues on this toolchain.
    // ClassDB::bind_method(D_METHOD("init_hand", "model_path"), &MediapipeBridge::init_hand);
    // ClassDB::bind_method(D_METHOD("init_holistic", "model_path"), &MediapipeBridge::init_holistic);

    ADD_SIGNAL(MethodInfo("landmarks_updated", PropertyInfo(Variant::INT, "hand_index"), PropertyInfo(Variant::PACKED_VECTOR3_ARRAY, "points")));
    ADD_SIGNAL(MethodInfo("gestures_updated", PropertyInfo(Variant::DICTIONARY, "gestures")));
}

bool MediapipeBridge::init_hand(const String &model_path) {
#ifdef _WIN32
    model_path_ = model_path;
    hand_mode_ = true;
    if (!hand_dll_) hand_dll_ = (void *)LoadLibraryW(L"Mediapipe_Hand_Tracking.dll");
    if (!hand_dll_) { UtilityFunctions::push_error("Load Mediapipe_Hand_Tracking.dll failed"); return false; }
    hand_init_ = (hand_init_t)GetProcAddress((HMODULE)hand_dll_, "Mediapipe_Hand_Tracking_Init");
    hand_detect_ = (hand_detect_frame_direct_t)GetProcAddress((HMODULE)hand_dll_, "Mediapipe_Hand_Tracking_Detect_Frame_Direct");
    if (!hand_init_ || !hand_detect_) { UtilityFunctions::push_error("Hand DLL exports not found"); return false; }
    int ok = hand_init_((const wchar_t*)model_path_.utf16().ptr());
    return ok == 0;
#else
    return false;
#endif
}

bool MediapipeBridge::init_holistic(const String &model_path) {
#ifdef _WIN32
    model_path_ = model_path;
    hand_mode_ = false;
    if (!holistic_dll_) holistic_dll_ = (void *)LoadLibraryW(L"MediapipeHolisticTracking.dll");
    if (!holistic_dll_) { UtilityFunctions::push_error("Load MediapipeHolisticTracking.dll failed"); return false; }
    hol_init_ = (holistic_init_t)GetProcAddress((HMODULE)holistic_dll_, "MediapipeHolisticTrackingInit");
    hol_detect_ = (holistic_detect_frame_direct_t)GetProcAddress((HMODULE)holistic_dll_, "MediapipeHolisticTrackingDetectFrameDirect");
    if (!hol_init_ || !hol_detect_) { UtilityFunctions::push_error("Holistic DLL exports not found"); return false; }
    int ok = hol_init_((const wchar_t*)model_path_.utf16().ptr());
    return ok == 0;
#else
    return false;
#endif
}

void MediapipeBridge::start_camera(int64_t device_index, int64_t w, int64_t h, int64_t fps) {
#ifdef VTUBERHUB_USE_OPENCV
    cam_index_ = static_cast<int>(device_index);
    cam_w_ = static_cast<int>(w);
    cam_h_ = static_cast<int>(h);
    cam_fps_ = static_cast<int>(fps);
    if (running_) return;
    running_ = true;
    worker_ = std::thread([this]() {
        cv::VideoCapture cap(cam_index_);
        cap.set(cv::CAP_PROP_FRAME_WIDTH, cam_w_);
        cap.set(cv::CAP_PROP_FRAME_HEIGHT, cam_h_);
        cap.set(cv::CAP_PROP_FPS, cam_fps_);
        if (!cap.isOpened()) {
            UtilityFunctions::push_error("OpenCV camera open failed");
            running_ = false; return;
        }
        std::vector<unsigned char> bgra;
        std::vector<double> out(21 * 3 * 2); // 21 points * 3 coords * 2 hands
        while (running_) {
            cv::Mat frame; cap >> frame; if (frame.empty()) continue;
            cv::Mat rgba; cv::cvtColor(frame, rgba, cv::COLOR_BGR2BGRA);
            bgra.assign(rgba.data, rgba.data + (rgba.cols * rgba.rows * 4));
            int ok = -1;
            if (hand_mode_ && hand_detect_) {
                ok = hand_detect_(bgra.data(), rgba.cols, rgba.rows, out.data(), (int)out.size());
            } else if (!hand_mode_ && hol_detect_) {
                ok = hol_detect_(bgra.data(), rgba.cols, rgba.rows, out.data(), (int)out.size());
            }
            if (ok == 0) {
                // Convert to PackedVector3Array (only first hand for demo)
                PackedVector3Array pts;
                pts.resize(21);
                for (int i = 0; i < 21; ++i) {
                    double x = out[i * 3 + 0];
                    double y = out[i * 3 + 1];
                    double z = out[i * 3 + 2];
                    pts.set(i, Vector3((real_t)x, (real_t)y, (real_t)z));
                }
                call_deferred("emit_landmarks_updated", 0, pts);
            }
        }
        cap.release();
    });
#else
    UtilityFunctions::push_warning("WITH_OPENCV=OFF or OpenCV not found: start_camera disabled");
#endif
}

void MediapipeBridge::stop() {
    if (running_) { running_ = false; }
    if (worker_.joinable()) worker_.join();
}

void MediapipeBridge::emit_landmarks_updated(int hand_index, const PackedVector3Array &points) {
    emit_signal("landmarks_updated", hand_index, points);
}

void MediapipeBridge::emit_gestures_updated(const Dictionary &gestures) {
    emit_signal("gestures_updated", gestures);
}