using System;
using Unity.VisualScripting;
using UnityEngine;

public class LinePlot : MonoBehaviour
{
    private UPose server;

    public int NumberOfPoints=61;
    public int PaddingSize=5;
    public int Speed=10;
    public float PlotWidth=2;
    public float LineWidth=0.02f;
    public Color LineColor=Color.green;
    private float slot=0;
    private LineRenderer line;
    public enum AngleChoice { LeftElbow, RightElbow, LeftKnee, RightKnee }
    public AngleChoice BodyAngle=AngleChoice.RightElbow;

    private float currentValue=0;

    void Start()
    {
        server = FindFirstObjectByType<UPose>();
        if (server == null)
        {
            Debug.LogError("You must have a Pipeserver in the scene!");
        }

        GameObject linePrefab = new GameObject("linePrefab");
        LineRenderer lineRenderer = linePrefab.AddComponent<LineRenderer>();
        lineRenderer.startWidth = LineWidth;
        lineRenderer.endWidth = LineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = LineColor;
        lineRenderer.endColor = LineColor;

        line = Instantiate(linePrefab).GetComponent<LineRenderer>();
        line.transform.parent = transform;
        line.positionCount = NumberOfPoints+((NumberOfPoints%2==0)?1:0);
        for(int i=0;i<line.positionCount;i++){

            line.SetPosition(i,new Vector3(getX(i),0,0));
        }
    }

    private float getX(int i){
        return PlotWidth*(i-(line.positionCount-1)/2f)/(0.5f*(line.positionCount-1));
    }

    private float getBodyAngle(){
        switch(BodyAngle){
            case AngleChoice.LeftElbow: return server.getLeftElbowAngle();
            case AngleChoice.RightElbow: return server.getRightElbowAngle();
            case AngleChoice.LeftKnee: return server.getLeftKneeAngle();
            case AngleChoice.RightKnee: return server.getRightKneeAngle();
            default:return 0;
        }
    }

    private int getNext(int i,int n){
        int j=i+n;
        for(;j>=line.positionCount;)j-=line.positionCount;
        return j;
    }

    // Update is called once per frame
    void Update()
    {
        int i=(int)Mathf.Floor(slot);
        if(i>=line.positionCount){
            i=0;
            slot=0;
        }

        //Smoothing
        currentValue=currentValue*0.95f+0.05f*getBodyAngle()/180f;

        line.SetPosition(i,new Vector3(getX(i),currentValue,0));
        slot+=Speed*Time.deltaTime;    

        for(int c=1;c<=PaddingSize;c++){
            int j=getNext(i,c);
            line.SetPosition(j,new Vector3(getX(j),0,0));
        }   
    }
}
