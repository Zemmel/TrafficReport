﻿using System;
using UnityEngine;
using TrafficReport;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;

namespace TrafficReport
{


	public class QueryToolGUIBase : MonoBehaviour
	{
		public enum HighlightType {
			None,
			Segment,
			Vehicle,
			Building,
			Citizen
		}

		Texture icon;
		Texture activeIcon;

		Config config;
		//int lastWidth;

		GameObject[] visualizations;
		Material lineMaterial;
		Material lineMaterialHighlight;

		Dictionary<string,int> highlightBreakdown;

		Dictionary<string, HashSet<uint>> typeMap;

		Report currentReport;
		uint currentHighlight;
		HighlightType currentHighlightType;

		public GUISkin uiSkin;
		public GUIStyle totalStyle;
		public GUIStyle buttonStyle;

		public bool leftHandDrive;

		Matrix4x4 guiScale;

		bool inDrag;
		Vector2 dragOffset;
		int lastScreenHeight;



		public QueryToolGUIBase()
		{   
			config = Config.Load ();
			currentHighlightType = HighlightType.None;
		}

		public virtual bool toolActive {
			get { return true; }
			set  { 
				Log.error("Function not overidden");
			}
		}

        public virtual bool guiVisible
        {
			get { return true; }
		}

        public void Awake()
        {

            icon = ResourceLoader.loadTexture(80, 80, "Materials/Button.png");
            activeIcon = ResourceLoader.loadTexture(80, 80, "Materials/Button.active.png");

			Log.info("Load Line Material...");

			Color red = new Color (1, 0, 0);
			Color gold = new Color (1, 0.9f, 0);

			string lineShader = ResourceLoader.loadResourceString ("Materials/Shaders/TransparentVertexLit.shader");

			lineMaterial = new Material (lineShader);
			lineMaterial.color = red;
			lineMaterial.SetColor("_Emission", red);
			lineMaterial.mainTexture = ResourceLoader.loadTexture(100, 200, "Materials/NewSkin.png");
			lineMaterial.renderQueue = 100;
			
			lineMaterialHighlight = new Material (lineMaterial);
			lineMaterialHighlight.color = gold;
			lineMaterialHighlight.SetColor("_Emission", gold);
			lineMaterial.renderQueue = 101;

			highlightBreakdown = new Dictionary<string,int>();


			Log.debug ("Gui initialized");
        }

		void MakeSkin() {

			Color highlight = new Color (20.0f / 255, 207.0f / 255, 248.0f / 255);

			uiSkin = (GUISkin)GUISkin.Instantiate (GUI.skin);
			uiSkin.window.normal.background = ResourceLoader.loadTexture(32, 32, "Materials/UIbg.png");
			uiSkin.window.border = new RectOffset (16, 16, 16, 16);
			uiSkin.window.padding = new RectOffset (12, 8, 26, 12);

			uiSkin.window.normal.textColor = highlight;
			uiSkin.window.alignment = TextAnchor.UpperCenter;
			uiSkin.window.fontSize = 30;
			uiSkin.window.fontStyle = FontStyle.Bold;

			uiSkin.window.onNormal = uiSkin.window.normal;
			uiSkin.window.onFocused = uiSkin.window.onNormal;
			uiSkin.window.onHover = uiSkin.window.onNormal;
			uiSkin.window.onActive = uiSkin.window.onNormal;

			uiSkin.button = new GUIStyle ();


			uiSkin.label.normal.textColor = Color.white;
			uiSkin.label.fontSize = 18;
			uiSkin.label.fontStyle = FontStyle.Bold;
			uiSkin.label.padding = new RectOffset (0, 0, 5, 5);

			uiSkin.toggle.normal.textColor = Color.white;
			uiSkin.toggle.fontSize = 18;
			uiSkin.toggle.fontStyle = FontStyle.Bold;
			uiSkin.toggle.padding = new RectOffset (20, 0, 0, 10);

			totalStyle = new GUIStyle (uiSkin.label);
			totalStyle.normal.textColor = highlight;
			totalStyle.fontSize = 20;
			totalStyle.fontStyle = FontStyle.Bold;


		}

		void Update() {


			//Animate the traffic lines
			lineMaterial.SetTextureOffset("_MainTex", new Vector2(Time.time * -0.5f, 0));
			lineMaterialHighlight.SetTextureOffset("_MainTex", new Vector2(Time.time * -0.5f, 0));

			if (lastScreenHeight != Screen.height) {
				float s = Screen.height / 1440.0f;
				guiScale = Matrix4x4.Scale (new Vector3 (s, s, s));
				lastScreenHeight = Screen.height;
			}


			Vector2 pos = Input.mousePosition;
			pos.x = pos.x * 1440.0f / Screen.height;
			pos.y = (Screen.height - pos.y) * 1440.0f / Screen.height;

			if (!inDrag && Input.GetMouseButtonDown (1) && config.buttonRect.Contains(pos) ) {
				inDrag = true;
				dragOffset = pos-config.buttonPosition;

			} else if (inDrag) {
				config.buttonPosition = pos-dragOffset;
				if(Input.GetMouseButtonUp(1)){
					inDrag=false;
					config.Save();
				}
			}
			
			if (Input.GetKeyUp(config.keyCode)){
				Log.info ("Toggling tool");
				toolActive = !toolActive;
			}

			if (toolActive && Input.GetKeyUp(KeyCode.Escape)) {
				toolActive = false;
			}
		}

		public void OnGUI()
		{
			if (!guiVisible) {
				return;
			}

			GUI.backgroundColor = Color.white;
			GUI.color = Color.white;
			GUI.contentColor = Color.white;

			GUI.matrix = guiScale;

			if (uiSkin == null) {
				MakeSkin();
			}

			GUI.skin = uiSkin;


			try 
			{
				if(GUI.Button(config.buttonRect, toolActive ? activeIcon : icon) && !inDrag) {
					Log.info ("Toggling tool");
					toolActive = !toolActive;
				}

				if (toolActive && currentReport != null) {

					Rect r = GUILayout.Window (50199, new Rect (20, 100, 200, 100), ReportSummary, "All Selected");
					if(currentHighlightType != HighlightType.None) {
						GUILayout.Window (50198, new Rect (240,100, 200, 100), HighlightSummary, "Highlighted");
					}
				}
			
			} catch(Exception e) {
				Log.error (e.Message);
				Log.error(e.StackTrace);
			}
			
			GUI.matrix = Matrix4x4.identity;
			GUI.skin = null;

		}
		
		void OnRenderObject() {

		}


		void ReportSummary (int id)
		{			

			GUILayout.Space (35);
			
			int remaining = currentReport.allEntities.Length;
			foreach (VehicleDisplay t in config.vehicleTypes) {

				int count = 0;
				if(typeMap.ContainsKey(t.id))
					count = typeMap[t.id].Count;

				remaining -= count;
				if(count > 0) {

					if(t.onOff != GUILayout.Toggle(t.onOff, t.display + ": " + count)) {
						SetTypeVisible(t.id, !t.onOff);
					}
				}
			}
			
			if(remaining > 0) {
				GUILayout.Label ("Other: " + remaining);
			}

			GUILayout.Label ("Total: " + currentReport.allEntities.Length,totalStyle);

		}

		void SetTypeVisible(string type, bool visible) {

			for(int i=0; i < config.vehicleTypes.Length; i++) {
				if(config.vehicleTypes[i].id == type) {
					config.vehicleTypes[i].onOff = visible;
				}
			}

			for (int i=0; i < currentReport.allEntities.Length; i++) {
				if(currentReport.allEntities[i].serviceType == type) {
					visualizations[i].GetComponent<MeshRenderer>().enabled = visible;
				}
			}

			config.Save ();
		}

		void HighlightSummary (int id)
		{			
			try 
			{
				GUILayout.Space (35);

				if(currentHighlightType == HighlightType.None) {
					GUILayout.Label("Nothing");
					return;
				}

							
				int remaining = highlightBreakdown ["total"];
				int total = remaining;

				foreach (VehicleDisplay t in config.vehicleTypes) {
					
					int count = 0;
					highlightBreakdown.TryGetValue(t.id, out count);
					if(count > 0) {
						remaining -= count;
						GUILayout.Label (t.display + ": " + count);
					}				
				}

				if(remaining > 0) {
					GUILayout.Label ("Other: " + remaining);
				}

				int percent = total * 100 / currentReport.allEntities.Length;
				GUILayout.Label ("Total: " + total + "     (" + percent + "%)",totalStyle );

			}catch(Exception e) {
				Log.error (e.Message);
				Log.error (e.StackTrace);
			}
		}

		public void SetReport(Report report) {

			if (visualizations != null) {
				RemoveAllPaths();
				currentReport = null;
				typeMap = null;
			}

			if (report == null || report.allEntities == null) {
				Log.debug ("Report NULL");
				return;
			}

			visualizations = new GameObject[report.allEntities.Length];
			for(int i=0; i < report.allEntities.Length; i++)
			{
				visualizations[i] =  CreatePathGameobject(report.allEntities[i].serviceType, report.allEntities[i].path);
			}
			
			float alpha = 30.0f / report.allEntities.Length;
			
			if (alpha > 1)
			{
				alpha = 1;
			}
			
			lineMaterial.color = new Color(1, 0, 0, alpha);

			GenerateMaps (report);

			currentReport = report;
		}

		private void GenerateMaps(Report report) {
			typeMap = new Dictionary<string, HashSet<uint>> ();

			for (uint i =0; i < report.allEntities.Length; i++) {

				string t = report.allEntities[i].serviceType;
				if(t == null){
					continue;
				}
				if(!typeMap.ContainsKey(t)){
					typeMap[t] = new HashSet<uint>();
				}
				typeMap[t].Add(i);
			}

		}

		public void SetSegmentHighlight(HighlightType type, uint id){

			if (currentReport == null) {
				return;
			}

			if (currentHighlight == id && currentHighlightType == type) {
				return;
			}

			foreach (GameObject go in visualizations) {
				go.GetComponent<Renderer> ().material = lineMaterial;
			}

			int total = 0;
			highlightBreakdown = new Dictionary<string,int>();

			
			for(int index=0; index < currentReport.allEntities.Length; index++) {

				bool highlighted = false;
				switch(type){
				case HighlightType.Segment:
				
					foreach(PathPoint p in currentReport.allEntities[index].path) {
						if(p.segmentID == id) {
							highlighted = true;
							break;
						}
					}

					break;
				case HighlightType.Building:
					if(currentReport.allEntities[index].sourceBuilding == id || currentReport.allEntities[index].targetBuilding == id) {

						highlighted = true;
					}
					break;
				case HighlightType.Vehicle:
					if(currentReport.allEntities[index].id == id && currentReport.allEntities[index].type == EntityType.Vehicle) {
						highlighted = true;
					}
					break;
				case HighlightType.Citizen:
					
					if(currentReport.allEntities[index].id == id && currentReport.allEntities[index].type == EntityType.Citzen) {
						highlighted = true;
					}
					break;
				}

				if(highlighted){
					visualizations [index].GetComponent<Renderer> ().material = lineMaterialHighlight;
					string t = currentReport.allEntities[index].serviceType;
					int count = 0;
					highlightBreakdown.TryGetValue(t, out count);
					count++;
					highlightBreakdown[t]=count;	
					total++;
				}
			}

			highlightBreakdown ["total"] = total;
			currentHighlight = id;
			currentHighlightType = type;
		}
		
		private GameObject CreatePathGameobject(string type, PathPoint[] positions) {
			
			lineMaterial.color = new Color(1, 0, 0, 1);
			
			PathMeshBuilder pb = new PathMeshBuilder();

			
			Vector3[] points = new Vector3[positions.Length];
			for (int i=0; i < positions.Length; i++) {
				points[i] = new Vector3(positions[i].x, positions[i].y, positions[i].z);
			}
			
			
			pb.AddPoints(points);
			
			Mesh m = pb.GetMesh();
			GameObject go = new GameObject(); ;
			go.AddComponent<MeshFilter>();
			go.AddComponent<MeshRenderer>();
			go.GetComponent<MeshFilter>().mesh = m;
			go.GetComponent<MeshFilter>().sharedMesh = m;
			go.GetComponent<MeshRenderer>().material = lineMaterial;
			go.transform.localPosition = new Vector3(0, 3, 0);

			foreach(VehicleDisplay t in config.vehicleTypes) {

				if(t.id == type) {
					go.GetComponent<MeshRenderer>().enabled = t.onOff;
				}
			}
			return go;
		}
		
		
		void RemoveAllPaths()
		{
			if (visualizations == null) {
				return;
			}
			
			foreach (GameObject v in visualizations)
			{
				GameObject.Destroy(v);
			}
			
			visualizations = null;
		}
	}
}

