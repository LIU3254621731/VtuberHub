#include <iostream> 
#include <opencv2/core/core.hpp> 
#include <opencv2/highgui/highgui.hpp> 
#include <opencv2/opencv.hpp>

#include "MediapipeHandTrackingDll.h"
#include "MediapipeHolisticTrackingDll.h"

std::string GetGestureResult(int result)
{
	std::string result_str = "δ֪����";
	switch (result)
	{
	case 1:
		result_str = "One";
		break;
	case 2:
		result_str = "Two";
		break;
	case 3:
		result_str = "Three";
		break;
	case 4:
		result_str = "Four";
		break;
	case 5:
		result_str = "Five";
		break;
	case 6:
		result_str = "Six";
		break;
	case 7:
		result_str = "ThumbUp";
		break;
	case 8:
		result_str = "Ok";
		break;
	case 9:
		result_str = "Fist";
		break;

	default:
		break;
	}

	return result_str;
}

std::string GetArmUpAndDownResult(int result)
{
	std::string result_str = "δ֪";
	switch (result)
	{
	case -1:
		result_str = "δ֪";
		break;
	case 1:
		result_str = "�ֱ�̧��";
		break;
	case 2:
		result_str = "�ֱ۷���";
		break;

	default:
		break;
	}

	return result_str;
}

void LandmarksCallBackImpl(int image_index, PoseInfo* infos, int count)
{
	std::cout << "image_index��" << image_index << std::endl;
	std::cout << "hand joint num��" << count << std::endl;
	//for (int i = 0; i < count; ++i)
	//{
	//	float x = infos[i].x;
	//	float y = infos[i].y;

	//	//std::cout << "x=" << x << "," << "y=" << y << endl;
	//}
}

void GestureResultCallBackImpl(int image_index, int* recogn_result, int count)
{
	std::cout << "image_index��" << image_index << std::endl;
	for (int i = 0; i < count; ++i)
	{
		std::cout << "��" << i << "ֻ�ֵ�ʶ����Ϊ��" << GetGestureResult(recogn_result[i]) <<std::endl;
	}
}

void HandTrackingDllTest()
{
	MediapipeHandTrackingDll mediapipeHandTrackingDll;
#ifdef _DEBUG
	std::string dll_path = ".././bin/MediapipeTest/x64/Debug/Mediapipe_Hand_Tracking.dll";
#else
	std::string dll_path = "./Mediapipe_Hand_Tracking.dll";
#endif // DEBUG

	mediapipeHandTrackingDll.LoadMediapipeHandTrackingDll(dll_path);
	mediapipeHandTrackingDll.GetAllFunctions();

	/* ��ʼ��Mediapipe Hand Tracking */
#ifdef _DEBUG
	std::string mediapipe_hand_tracking_model_path = ".././bin/MediapipeTest/x64/Debug/hand_tracking_desktop_live.pbtxt";
#else
	std::string mediapipe_hand_tracking_model_path = "./hand_tracking_desktop_live.pbtxt";
#endif // _DEBUG

	if (mediapipeHandTrackingDll.m_Mediapipe_Hand_Tracking_Init(mediapipe_hand_tracking_model_path.c_str()))
	{
		std::cout << "��ʼ��ģ�ͳɹ�" << std::endl;
	}
	else
	{
		std::cout << "��ʼ��ģ��ʧ��" << std::endl;
	}

	// ע��ص�����
	if (mediapipeHandTrackingDll.m_Mediapipe_Hand_Tracking_Reigeter_Landmarks_Callback(LandmarksCallBackImpl))
	{
		std::cout << "ע������ص������ɹ�" << std::endl;
	}
	else
	{
		std::cout << "ע������ص�����ʧ��" << std::endl;
	}

	if (mediapipeHandTrackingDll.m_Mediapipe_Hand_Tracking_Register_Gesture_Result_Callback(GestureResultCallBackImpl))
	{
		std::cout << "ע������ʶ�����ص������ɹ�" << std::endl;
	}
	else
	{
		std::cout << "ע������ʶ�����ص�����ʧ��" << std::endl;
	}

	/* 1 ��һ�ַ�ʽ��������Ƶ·��ʶ����Ƶ�е����� */

	//std::string test_video_path = "pjd-0028-Ch.mp4";
	//Mediapipe_Hand_Tracking_Detect_Video(test_video_path.c_str(), 1);


	//�򿪵�һ������ͷ
	cv::VideoCapture cap(0);
	//�ж�����ͷ�Ƿ��
	if (!cap.isOpened())
	{
		std::cout << "����ͷδ�ɹ���" << std::endl;
	}

	//��������
	cv::namedWindow("������ͷ", 1);

	int image_index = 0;
	while (1)
	{
		//����Mat����
		cv::Mat frame;
		//��cap�ж�ȡһ֡�浽frame��
		bool res = cap.read(frame);
		if (!res)
		{
			break;
		}

		//�ж��Ƿ��ȡ��
		if (frame.empty())
		{
			break;
		}

		//���
		cv::Mat copyMat;
		frame.copyTo(copyMat);

		// ǳ����
		/*Mat copyMat;
		copyMat = frame;*/

		uchar* pImageData = copyMat.data;


		/* 2 �ڶ��ַ�ʽ��������Ƶ֡��ͨ���ص������ص���� */
		//if (Mediapipe_Hand_Tracking_Detect_Frame(image_index, copyMat.cols, copyMat.rows, (void*)pImageData))
		//{
		//	//std::cout << "Mediapipe_Hand_Tracking_Detect_Frameִ�гɹ���" << std::endl;
		//}
		//else
		//{
		//	std::cout << "Mediapipe_Hand_Tracking_Detect_Frameִ��ʧ�ܣ�" << std::endl;
		//}

		/* 3 �����ַ�ʽ��������Ƶֱ֡�ӷ�������ʶ��������ͨ���ص��������ؽ�� */
		GestureRecognitionResult gestureRecognitionResult;
		if (mediapipeHandTrackingDll.m_Mediapipe_Hand_Tracking_Detect_Frame_Direct(copyMat.cols, copyMat.rows, (void*)pImageData, gestureRecognitionResult))
		{
			for (int i = 0; i < 2; ++i)
			{
				if (gestureRecognitionResult.m_Gesture_Recognition_Result[i] != -1)
				{
					std::cout << "��" << i << "ֻ�ֵ�����ʶ����Ϊ��" << GetGestureResult(gestureRecognitionResult.m_Gesture_Recognition_Result[i]) << std::endl;
				}

				if (gestureRecognitionResult.m_HandUp_HandDown_Detect_Result[i] != -1)
				{
					std::cout << "��" << i << "ֻ�ֵ�̧�ַ���ʶ����Ϊ��" << GetArmUpAndDownResult(gestureRecognitionResult.m_HandUp_HandDown_Detect_Result[i]) << std::endl;
				}
			}

		}
		else
		{
			std::cout << "Mediapipe_Hand_Tracking_Detect_Frame_Directִ��ʧ�ܣ�" << std::endl;
		}


		//��ʾ����ͷ��ȡ����ͼ��
		imshow("������ͷ", frame);
		//�ȴ�1���룬����������˳�ѭ��
		if (cv::waitKey(1) >= 0)
		{
			break;
		}

		image_index += 1;
	}

	if (mediapipeHandTrackingDll.m_Mediapipe_Hand_Tracking_Release())
	{
		std::cout << "Mediapipe�ͷųɹ���" << std::endl;
	}
	else
	{
		std::cout << "Mediapipe�ͷ�ʧ�ܣ�" << std::endl;
	}


	cap.release();
	cv::destroyAllWindows();

	mediapipeHandTrackingDll.UnLoadMediapipeHandTrackingDll();
}


void HolisticTrackingDllTest()
{
	MediapipeHolisticTrackingDll mediapipeHolisticTrackingDll;

#ifdef _DEBUG
	std::string dll_path = ".././bin/MediapipeTest/x64/Debug/MediapipeHolisticTracking.dll";
#else
	std::string dll_path = "./MediapipeHolisticTracking.dll";
#endif // DEBUG

	mediapipeHolisticTrackingDll.LoadMediapipeHolisticTrackingDll(dll_path);
	mediapipeHolisticTrackingDll.GetAllFunctions();


	/* ��ʼ��Mediapipe Holistic Tracking */
#ifdef _DEBUG
	std::string mediapipe_holistic_tracking_model_path = ".././bin/MediapipeTest/x64/Debug/holistic_tracking_cpu.pbtxt";
#else
	std::string mediapipe_holistic_tracking_model_path = "./holistic_tracking_cpu.pbtxt";
#endif // _DEBUG

	if (mediapipeHolisticTrackingDll.m_MediapipeHolisticTrackingInit(mediapipe_holistic_tracking_model_path.c_str(), true,true,true,false))
	{
		std::cout << "初始化模型成功" << std::endl;
	}
	else
	{
		std::cout << "初始化模型失败" << std::endl;
	}

	/*----- ��һ�ַ�ʽ����ͼƬ֡��ȥʶ�� -----*/

	//�򿪵�һ������ͷ
	cv::VideoCapture cap(0);
	//设置摄像头基础参数以匹配常见模型输入
	cap.set(cv::CAP_PROP_FRAME_WIDTH, 640);
	cap.set(cv::CAP_PROP_FRAME_HEIGHT, 480);
	cap.set(cv::CAP_PROP_FPS, 30);
	//判断摄像头是否打开
	if (!cap.isOpened())
	{
		std::cout << "摄像头未成功打开" << std::endl;
	}

	//��������
	cv::namedWindow("������ͷ", 1);

	while (true)
	{
		//����Mat����
		cv::Mat frame;
		//��cap�ж�ȡһ֡�浽frame��
		bool res = cap.read(frame);
		if (!res)
		{
			break;
		}

		//�ж��Ƿ��ȡ��
		if (frame.empty())
		{
			break;
		}

		//���
		cv::Mat copyMat;
		frame.copyTo(copyMat);
		cv::cvtColor(copyMat, copyMat, cv::COLOR_BGR2RGB);
		if (!copyMat.isContinuous()) { copyMat = copyMat.clone(); }

		uchar* pImageData = copyMat.data;
		int* pdetect_result = new int[4];
		if (mediapipeHolisticTrackingDll.m_MediapipeHolisticTrackingDetectFrameDirect(copyMat.cols, copyMat.rows, (void*)pImageData, pdetect_result,true))
		{
			std::string leftArmUpAndDownRecognitionResult = GetArmUpAndDownResult(pdetect_result[0]);
			std::string rightArmUpAndDownRecognitionResult = GetArmUpAndDownResult(pdetect_result[1]);
			std::string leftHandGestureRecognitionResult = GetGestureResult(pdetect_result[2]);
			std::string rightHandGestureRecognitionResult = GetGestureResult(pdetect_result[3]);

			std::cout << "����̧�ַ��ֽ��Ϊ��" << leftArmUpAndDownRecognitionResult << std::endl;
			std::cout << "����̧�ַ��ֽ��Ϊ��" << rightArmUpAndDownRecognitionResult << std::endl;
			std::cout << "��������Ϊ��" << leftHandGestureRecognitionResult << std::endl;
			std::cout << "��������Ϊ��" << rightHandGestureRecognitionResult << std::endl;
		}
		else
		{
			std::cout << "Mediapipe_Holistic_Tracking_Detect_Frame_Directִ��ʧ�ܣ�" << std::endl;
		}
		delete[] pdetect_result;

		//��ʾ����ͷ��ȡ����ͼ��
		imshow("������ͷ", frame);
		//�ȴ�1���룬����������˳�ѭ��
		if (cv::waitKey(1) >= 0)
		{
			break;
		}
	}

	/*----- �ڶ��ַ�ʽ����DLL�ڲ�������ͷ����ʶ����Ҫ������ -----*/
	//mediapipeHolisticTrackingDll.m_Mediapipe_Holistic_Tracking_Detect_Camera(true);

	if (mediapipeHolisticTrackingDll.m_MediapipeHolisticTrackingRelease())
	{
		std::cout << "Mediapipe�ͷųɹ���" << std::endl;
	}
	else
	{
		std::cout << "Mediapipe�ͷ�ʧ�ܣ�" << std::endl;
	}


	cap.release();
	cv::destroyAllWindows();


	mediapipeHolisticTrackingDll.UnLoadMediapipeHolisticTrackingDll();
}

int main()
{
	//HandTrackingDllTest();

	HolisticTrackingDllTest();

	getchar();

	return 0;
}