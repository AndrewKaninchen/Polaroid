using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Asyncoroutine;
using System.Threading.Tasks;

public class PolaroidAnimation : MonoBehaviour
{
    public Transform photoTransform;
    
    public Transform cameraHoldingPosition;
    public Transform cameraShootingPosition;
    public Transform cameraPrintingPosition;
    public Transform photoPrintingInsidePosition;
    public Transform photoPrintingOutsidePosition;
    public Transform photoPlacingPosition;
    public Transform photoPreviewPosition;
    
    public States state;

    public Polaroid polaroidComponent;
    public enum States
    {
        HoldingCamera,
        Shooting,
        Printing,
        PlacingPhoto
    }
    
    private async void Start()
    {
        state = States.HoldingCamera;
        await HoldCamera();
    }
  
    private async void Update()
    {
        switch (state)
        {
            case States.HoldingCamera:
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    await Aim();
                    state = States.Shooting;
                }
                break;
            case States.Shooting:
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    await HoldCamera();
                    state = States.HoldingCamera;
                }
                
                if (Input.GetMouseButtonDown(0))
                {
                    polaroidComponent.Snapshot();
                    state = States.Printing;
                    await PrintPhoto();
                    state = States.PlacingPhoto;
                }
                break;
            case States.Printing:
                break;
            case States.PlacingPhoto:
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    polaroidComponent.Place();
                    await HoldCamera();
                    state = States.HoldingCamera;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
        }
    }
    
    private async Task HoldCamera()
    {
        photoTransform.SetParent(photoPreviewPosition);
        photoTransform.localPosition = Vector3.zero;
        photoTransform.localScale = Vector3.one;

        var sequence = DOTween.Sequence();
        sequence.Append(transform.DOLocalMove(cameraHoldingPosition.localPosition, 0.2f));
        sequence.Join(transform.DOScale(cameraHoldingPosition.localScale, 0.2f));
        sequence.Join(transform.DOLocalRotateQuaternion(cameraHoldingPosition.localRotation, 0.2f));
        await sequence.IsComplete();
    }

    private async Task PrintPhoto()
    {
        var sequence = DOTween.Sequence();
        sequence.Append(transform.DOLocalMove(cameraPrintingPosition.localPosition, 0.4f));
        sequence.Join(transform.DOScale(cameraPrintingPosition.localScale, 0.4f));
        sequence.Join(transform.DOLocalRotateQuaternion(cameraPrintingPosition.localRotation, 0.4f));
        await sequence.IsComplete();
        
        photoTransform.SetParent(photoPrintingInsidePosition);
        photoTransform.localPosition = Vector3.zero;
        photoTransform.localScale = Vector3.one;
        photoTransform.localRotation = Quaternion.identity;
        
        photoTransform.SetParent(transform);

        await new WaitForSeconds(0.2f);
        await photoTransform.DOLocalMove(photoPrintingOutsidePosition.localPosition, 1.2f).IsComplete();
        
        var sequence2 = DOTween.Sequence();
        
        photoTransform.SetParent(photoPlacingPosition);
        
        sequence2.Append(photoTransform.DOLocalMove(Vector3.zero, 1f));
        sequence2.Join(photoTransform.DOScale(Vector3.one, 1f));
        sequence2.Join(photoTransform.DOLocalRotateQuaternion(Quaternion.identity, 1f));
        await sequence2.IsComplete();
        
        var sequence3 = DOTween.Sequence();
        sequence3.Append(transform.DOLocalMove(cameraHoldingPosition.localPosition, 0.4f));
        sequence3.Join(transform.DOScale(cameraHoldingPosition.localScale, 0.4f));
        sequence3.Join(transform.DOLocalRotateQuaternion(cameraHoldingPosition.localRotation, 0.4f));
        await sequence3.IsComplete();
    }

    private async Task Aim()
    {
        photoTransform.SetParent(photoPreviewPosition);
        photoTransform.localPosition = Vector3.zero;
        photoTransform.localScale = Vector3.one;
        photoTransform.localRotation = Quaternion.identity;

        var sequence = DOTween.Sequence();
        sequence.Append(transform.DOLocalMove(cameraShootingPosition.localPosition, 0.2f));
        sequence.Join(transform.DOScale(cameraShootingPosition.localScale, 0.2f));
        sequence.Join(transform.DOLocalRotateQuaternion(cameraShootingPosition.localRotation, 0.2f));
        await sequence.IsComplete();
    }
}
