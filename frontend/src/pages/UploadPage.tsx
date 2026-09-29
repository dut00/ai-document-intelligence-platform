import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState, type DragEvent } from 'react'
import { useNavigate } from 'react-router'
import { documentKeys, uploadDocument } from '../api/documents'
import { ErrorAlert } from '../components/ErrorAlert'
import { Spinner } from '../components/Spinner'
import { acceptedFileTypes, validateFile } from '../lib/fileValidation'
import { formatFileSize } from '../lib/format'

export function UploadPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const inputRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [dragging, setDragging] = useState(false)

  const upload = useMutation({
    mutationFn: uploadDocument,
    onSuccess: async (document) => {
      await queryClient.invalidateQueries({ queryKey: documentKeys.all })
      // The analysis runs in the background; the details page updates live when it finishes.
      await navigate(`/documents/${document.id}`)
    },
  })

  function select(selected: File | undefined) {
    upload.reset()
    setFile(selected ?? null)
    setValidationError(selected ? validateFile(selected) : null)
  }

  function handleDrop(event: DragEvent<HTMLElement>) {
    event.preventDefault()
    setDragging(false)
    select(event.dataTransfer.files[0])
  }

  const canUpload = file !== null && validationError === null && !upload.isPending

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Upload a document</h1>
        <p className="mt-1 text-sm text-slate-600">
          PDF or plain text, up to 10 MB. Contracts, invoices, letters: the analysis extracts the summary,
          parties, important dates, amounts and risks.
        </p>
      </div>

      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        onDragOver={(event) => {
          event.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={handleDrop}
        className={`flex w-full flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed px-6 py-12 text-center transition-colors ${
          dragging ? 'border-indigo-500 bg-indigo-50' : 'border-slate-300 bg-white hover:border-slate-400'
        }`}
      >
        <span className="text-sm font-medium text-slate-900">Drop a file here or click to browse</span>
        <span className="text-xs text-slate-500">.pdf, .txt</span>
      </button>
      <input
        ref={inputRef}
        type="file"
        accept={acceptedFileTypes}
        className="hidden"
        onChange={(event) => {
          select(event.target.files?.[0])
          // Allow picking the same file again after an error.
          event.target.value = ''
        }}
      />

      {file && (
        <div className="flex items-center justify-between gap-4 rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm">
          <span className="truncate font-medium">{file.name}</span>
          <span className="shrink-0 text-slate-500">{formatFileSize(file.size)}</span>
        </div>
      )}

      <ErrorAlert error={validationError ? new Error(validationError) : upload.error} />

      <button
        type="button"
        disabled={!canUpload}
        onClick={() => file && upload.mutate(file)}
        className="flex items-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {upload.isPending && <Spinner />}
        Upload
      </button>
    </div>
  )
}
