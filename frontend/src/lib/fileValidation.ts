// Mirrors the API's upload rules, so the user gets feedback before sending the file. The API
// still validates everything, including the file signature.

export const maxFileSizeBytes = 10 * 1024 * 1024

const allowedTypes: Record<string, string> = {
  '.pdf': 'application/pdf',
  '.txt': 'text/plain',
}

export const acceptedFileTypes = Object.keys(allowedTypes).join(',')

export function validateFile(file: File): string | null {
  const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase()
  const expectedType = allowedTypes[extension]

  if (!expectedType) {
    return 'Only PDF and plain text (.txt) files are supported.'
  }

  // Browsers derive the type from the extension; an empty type (unknown to the OS) is left to the API.
  if (file.type && file.type !== expectedType) {
    return 'The file type does not match its extension.'
  }

  if (file.size === 0) {
    return 'The file is empty.'
  }

  if (file.size > maxFileSizeBytes) {
    return 'The file must not exceed 10 MB.'
  }

  return null
}

/** The file with a content type the API accepts, for when the OS knows no type for the extension. */
export function withContentType(file: File): File {
  if (file.type) {
    return file
  }

  const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase()
  return new File([file], file.name, { type: allowedTypes[extension] ?? 'application/octet-stream' })
}
